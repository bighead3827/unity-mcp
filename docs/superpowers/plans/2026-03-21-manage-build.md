# manage_build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `manage_build` MCP tool with 8 actions (build, status, platform, settings, scenes, profiles, batch, cancel) for AI-driven Unity player builds.

**Architecture:** Single Python MCP tool → C# HandleCommand with action dispatch. Builds run async via `EditorApplication.delayCall` + `PendingResponse` polling. Version-dependent code uses `#if UNITY_6000_0_OR_NEWER` for Build Profiles and reflection for `NamedBuildTarget` availability.

**Tech Stack:** Python (FastMCP, Click), C# (Unity Editor API, Newtonsoft.Json)

**Spec:** `docs/superpowers/specs/2026-03-21-manage-build-design.md`

---

## File Structure

### Files to Create

| File | Responsibility |
|------|---------------|
| `Server/src/services/tools/manage_build.py` | Python MCP tool — action validation, param forwarding |
| `Server/tests/test_manage_build.py` | Python tests — mock transport, action/param validation |
| `Server/src/cli/commands/build.py` | CLI commands — Click group mirroring tool actions |
| `MCPForUnity/Editor/Tools/ManageBuild.cs` | C# HandleCommand — action dispatch to helpers |
| `MCPForUnity/Editor/Tools/Build/BuildJob.cs` | Job state machine + static job store |
| `MCPForUnity/Editor/Tools/Build/BuildRunner.cs` | Async build execution via delayCall |
| `MCPForUnity/Editor/Tools/Build/BuildTargetMapping.cs` | Friendly name → BuildTarget/BuildTargetGroup/NamedBuildTarget |
| `MCPForUnity/Editor/Tools/Build/BuildSettingsHelper.cs` | PlayerSettings read/write with version-safe APIs |

### Files to Modify

| File | Change |
|------|--------|
| `MCPForUnity/Editor/Tools/McpForUnityToolAttribute.cs` | Add `MaxPollSeconds` property |
| `MCPForUnity/Editor/Services/IToolDiscoveryService.cs` | Add `MaxPollSeconds` to `ToolInfo` |
| `MCPForUnity/Editor/Services/ToolDiscoveryService.cs` | Read `MaxPollSeconds` from attribute |
| `MCPForUnity/Editor/Services/Transport/Transports/WebSocketTransportClient.cs` | Serialize `max_poll_seconds` in tool metadata |
| `Server/src/models/models.py` | Add `max_poll_seconds` to `ToolDefinitionModel` |
| `Server/src/services/custom_tool_service.py` | Pass `max_poll_seconds` to `_poll_until_complete` |
| `Server/src/cli/main.py` | Register `build` CLI command group |

### Unity Version Compatibility

These `#if` guards are required in C# code:

| API | Guard | Fallback |
|-----|-------|----------|
| `BuildProfile`, `BuildPlayerWithProfileOptions` | `#if UNITY_6000_0_OR_NEWER` | Return error: "Build Profiles require Unity 6+" |
| `BuildReport.GetLatestReport()` | `#if UNITY_6000_0_OR_NEWER` | Fall through to "No build jobs found" |
| `BuildReport.SummarizeErrors()` | `#if UNITY_2022_3_OR_NEWER` | Manual error extraction from `BuildReport.steps` |
| `PlayerSettings.*(NamedBuildTarget)` | Always use (available since 2021.2, our min version) | N/A |
| `PlayerSettings.*ForGroup(BuildTargetGroup)` | Never use — deprecated, generates warnings | N/A |

---

## Task 1: Extended Polling Timeout Infrastructure

Add `max_poll_seconds` support to the polling pipeline so `manage_build` can use 30-minute timeouts.

**Files:**
- Modify: `MCPForUnity/Editor/Tools/McpForUnityToolAttribute.cs`
- Modify: `MCPForUnity/Editor/Services/IToolDiscoveryService.cs`
- Modify: `MCPForUnity/Editor/Services/ToolDiscoveryService.cs`
- Modify: `MCPForUnity/Editor/Services/Transport/Transports/WebSocketTransportClient.cs`
- Modify: `Server/src/models/models.py`
- Modify: `Server/src/services/custom_tool_service.py`

- [ ] **Step 1: Add `MaxPollSeconds` to C# attribute**

In `MCPForUnity/Editor/Tools/McpForUnityToolAttribute.cs`, after the `PollAction` property (around line 52), add:

```csharp
/// <summary>
/// Maximum seconds to poll before timing out. 0 means use the server default.
/// Useful for long-running operations like builds.
/// </summary>
public int MaxPollSeconds { get; set; } = 0;
```

- [ ] **Step 2: Add `MaxPollSeconds` to `ToolInfo`**

In `MCPForUnity/Editor/Services/IToolDiscoveryService.cs`, after `PollAction` (line 19), add:

```csharp
public int MaxPollSeconds { get; set; } = 0;
```

- [ ] **Step 3: Read `MaxPollSeconds` in ToolDiscoveryService**

In `MCPForUnity/Editor/Services/ToolDiscoveryService.cs`, in the tool info construction block (around line 134-136), add after the `PollAction` line:

```csharp
MaxPollSeconds = toolAttr.MaxPollSeconds,
```

- [ ] **Step 4: Serialize `max_poll_seconds` in WebSocket transport**

In `MCPForUnity/Editor/Services/Transport/Transports/WebSocketTransportClient.cs`, in the tool serialization block (around line 542-544), add after `["poll_action"]`:

```csharp
["max_poll_seconds"] = tool.MaxPollSeconds,
```

- [ ] **Step 5: Add `max_poll_seconds` to Python `ToolDefinitionModel`**

In `Server/src/models/models.py`, after `poll_action` (line 30), add:

```python
max_poll_seconds: int = 0
```

- [ ] **Step 6: Pass `max_poll_seconds` to `_poll_until_complete`**

In `Server/src/services/custom_tool_service.py`:

Update `_poll_until_complete` signature (around line 182) to accept `max_poll_seconds`:

```python
async def _poll_until_complete(
    self,
    tool_name: str,
    unity_instance,
    initial_params: dict[str, object],
    initial_response,
    poll_action: str,
    user_id: str | None = None,
    max_poll_seconds: int = 0,
) -> MCPResponse:
```

Change the deadline line (around line 194) from:
```python
deadline = time.time() + _MAX_POLL_SECONDS
```
to:
```python
timeout = max_poll_seconds if max_poll_seconds > 0 else _MAX_POLL_SECONDS
deadline = time.time() + timeout
```

Update the call site in `execute_tool` (around line 158) to pass the value:
```python
result = await self._poll_until_complete(
    tool_name,
    unity_instance,
    params,
    response,
    definition.poll_action or "status",
    user_id=user_id,
    max_poll_seconds=definition.max_poll_seconds or 0,
)
```

- [ ] **Step 7: Commit**

```bash
git add MCPForUnity/Editor/Tools/McpForUnityToolAttribute.cs \
       MCPForUnity/Editor/Services/IToolDiscoveryService.cs \
       MCPForUnity/Editor/Services/ToolDiscoveryService.cs \
       MCPForUnity/Editor/Services/Transport/Transports/WebSocketTransportClient.cs \
       Server/src/models/models.py \
       Server/src/services/custom_tool_service.py
git commit -m "feat: add max_poll_seconds to polling pipeline for long-running tools"
```

---

## Task 2: C# BuildTargetMapping Helper

Maps friendly target names from Python to Unity's `BuildTarget`, `BuildTargetGroup`, and `NamedBuildTarget`.

**Files:**
- Create: `MCPForUnity/Editor/Tools/Build/BuildTargetMapping.cs`

- [ ] **Step 1: Create the mapping helper**

```csharp
using UnityEditor;
using UnityEditor.Build;

namespace MCPForUnity.Editor.Tools.Build
{
    public static class BuildTargetMapping
    {
        /// <summary>
        /// Resolve a friendly target name (e.g., "windows64") to a BuildTarget enum value.
        /// Also accepts raw enum names (e.g., "StandaloneWindows64") as fallback.
        /// </summary>
        public static bool TryResolveBuildTarget(string name, out BuildTarget target)
        {
            if (string.IsNullOrEmpty(name))
            {
                target = EditorUserBuildSettings.activeBuildTarget;
                return true;
            }

            switch (name.ToLowerInvariant())
            {
                case "windows64": target = BuildTarget.StandaloneWindows64; return true;
                case "windows": case "windows32": target = BuildTarget.StandaloneWindows; return true;
                case "osx": case "macos": target = BuildTarget.StandaloneOSX; return true;
                case "linux64": case "linux": target = BuildTarget.StandaloneLinux64; return true;
                case "android": target = BuildTarget.Android; return true;
                case "ios": target = BuildTarget.iOS; return true;
                case "webgl": target = BuildTarget.WebGL; return true;
                case "uwp": target = BuildTarget.WSAPlayer; return true;
                case "tvos": target = BuildTarget.tvOS; return true;
                // VisionOS requires a late 2022.3 patch or Unity 6+; guard broadly
#if UNITY_2022_3_OR_NEWER
                case "visionos": target = BuildTarget.VisionOS; return true;
#endif
                default:
                    // Try parsing as raw enum name
                    if (System.Enum.TryParse(name, true, out target))
                        return true;
                    target = default;
                    return false;
            }
        }

        /// <summary>
        /// Get the BuildTargetGroup for a BuildTarget.
        /// Required by SwitchActiveBuildTarget and BuildPlayerOptions.
        /// </summary>
        public static BuildTargetGroup GetTargetGroup(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return BuildTargetGroup.Standalone;
                case BuildTarget.iOS: return BuildTargetGroup.iOS;
                case BuildTarget.Android: return BuildTargetGroup.Android;
                case BuildTarget.WebGL: return BuildTargetGroup.WebGL;
                case BuildTarget.WSAPlayer: return BuildTargetGroup.WSAPlayer;
                case BuildTarget.tvOS: return BuildTargetGroup.tvOS;
                default: return BuildTargetGroup.Unknown;
            }
        }

        /// <summary>
        /// Get a NamedBuildTarget from a BuildTarget.
        /// Used for PlayerSettings APIs (always prefer NamedBuildTarget over BuildTargetGroup).
        /// </summary>
        public static NamedBuildTarget GetNamedBuildTarget(BuildTarget target)
        {
            return NamedBuildTarget.FromBuildTargetGroup(GetTargetGroup(target));
        }

        /// <summary>
        /// Resolve a friendly target name directly to a NamedBuildTarget.
        /// Returns null string on success, error message on failure.
        /// </summary>
        public static string TryResolveNamedBuildTarget(string name, out NamedBuildTarget namedTarget)
        {
            if (!TryResolveBuildTarget(name, out var buildTarget))
            {
                namedTarget = default;
                return $"Unknown build target: '{name}'. Valid targets: windows64, osx, linux64, android, ios, webgl, uwp, tvos, visionos";
            }
            namedTarget = GetNamedBuildTarget(buildTarget);
            return null;
        }

        /// <summary>
        /// Get the default output filename for a build target.
        /// </summary>
        public static string GetDefaultOutputPath(BuildTarget target, string productName)
        {
            string basePath = $"Builds/{target}";
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return $"{basePath}/{productName}.exe";
                case BuildTarget.StandaloneOSX:
                    return $"{basePath}/{productName}.app";
                case BuildTarget.StandaloneLinux64:
                    return $"{basePath}/{productName}.x86_64";
                case BuildTarget.Android:
                    return EditorUserBuildSettings.buildAppBundle
                        ? $"{basePath}/{productName}.aab"
                        : $"{basePath}/{productName}.apk";
                case BuildTarget.iOS:
                case BuildTarget.WebGL:
                    return $"{basePath}/{productName}";
                default:
                    return $"{basePath}/{productName}";
            }
        }

        /// <summary>
        /// Resolve a subtarget string ("player" or "server") to the int value for BuildPlayerOptions.subtarget.
        /// </summary>
        public static int ResolveSubtarget(string subtarget)
        {
            if (string.IsNullOrEmpty(subtarget) || subtarget.ToLowerInvariant() == "player")
                return (int)StandaloneBuildSubtarget.Player;
            if (subtarget.ToLowerInvariant() == "server")
                return (int)StandaloneBuildSubtarget.Server;
            return (int)StandaloneBuildSubtarget.Player;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add MCPForUnity/Editor/Tools/Build/BuildTargetMapping.cs
git commit -m "feat: add BuildTargetMapping helper for friendly target name resolution"
```

---

## Task 3: C# BuildJob State Machine

Tracks build job lifecycle for async polling.

**Files:**
- Create: `MCPForUnity/Editor/Tools/Build/BuildJob.cs`

- [ ] **Step 1: Create the BuildJob class**

```csharp
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace MCPForUnity.Editor.Tools.Build
{
    public enum BuildJobState
    {
        Pending,
        Building,
        Succeeded,
        Failed,
        Cancelled,
        Skipped
    }

    public class BuildJob
    {
        public string JobId { get; }
        public BuildJobState State { get; set; } = BuildJobState.Pending;
        public BuildTarget Target { get; set; }
        public string OutputPath { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public BuildReport Report { get; set; }
        public string ErrorMessage { get; set; }

        public BuildJob(string jobId, BuildTarget target, string outputPath)
        {
            JobId = jobId;
            Target = target;
            OutputPath = outputPath;
        }

        public object ToStatusResponse()
        {
            var data = new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["platform"] = Target.ToString(),
                ["output_path"] = OutputPath
            };

            if (StartedAt != default)
                data["started_at"] = StartedAt.ToString("O");

            if (CompletedAt.HasValue)
            {
                data["duration_seconds"] = (CompletedAt.Value - StartedAt).TotalSeconds;
                data["completed_at"] = CompletedAt.Value.ToString("O");
            }

            if (Report != null)
            {
                var summary = Report.summary;
                data["total_size_mb"] = Math.Round(summary.totalSize / (1024.0 * 1024.0), 2);
                data["errors"] = summary.totalErrors;
                data["warnings"] = summary.totalWarnings;
            }

            if (!string.IsNullOrEmpty(ErrorMessage))
                data["error"] = ErrorMessage;

            return data;
        }
    }

    public class BatchJob
    {
        public string JobId { get; }
        public BuildJobState State { get; set; } = BuildJobState.Pending;
        public List<BuildJob> Children { get; } = new();
        public int CurrentIndex { get; set; } = -1;

        public BatchJob(string jobId)
        {
            JobId = jobId;
        }

        public object ToStatusResponse()
        {
            int completed = 0;
            string currentBuild = null;
            var builds = new List<object>();

            foreach (var child in Children)
            {
                if (child.State == BuildJobState.Succeeded || child.State == BuildJobState.Failed)
                    completed++;
                if (child.State == BuildJobState.Building)
                    currentBuild = child.JobId;
                builds.Add(child.ToStatusResponse());
            }

            return new Dictionary<string, object>
            {
                ["job_id"] = JobId,
                ["result"] = State.ToString().ToLowerInvariant(),
                ["completed"] = completed,
                ["total"] = Children.Count,
                ["current_build"] = currentBuild,
                ["builds"] = builds
            };
        }
    }

    /// <summary>
    /// Static store for all build jobs. Note: static fields are cleared on domain reload,
    /// but this is acceptable because BuildPipeline.BuildPlayer blocks the editor thread,
    /// preventing domain reload during a build. For batch builds with platform switches,
    /// the batch scheduling happens after each build completes (via delayCall), so state
    /// is maintained within a single domain lifecycle.
    /// </summary>
    public static class BuildJobStore
    {
        private static readonly Dictionary<string, BuildJob> _buildJobs = new();
        private static readonly Dictionary<string, BatchJob> _batchJobs = new();
        private static BuildJob _lastCompletedJob;

        public static string CreateJobId() => $"build-{Guid.NewGuid():N}".Substring(0, 16);
        public static string CreateBatchId() => $"batch-{Guid.NewGuid():N}".Substring(0, 16);

        public static void AddBuildJob(BuildJob job) => _buildJobs[job.JobId] = job;
        public static void AddBatchJob(BatchJob job) => _batchJobs[job.JobId] = job;

        public static BuildJob GetBuildJob(string jobId)
        {
            _buildJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public static BatchJob GetBatchJob(string jobId)
        {
            _batchJobs.TryGetValue(jobId, out var job);
            return job;
        }

        public static BuildJob LastCompletedJob => _lastCompletedJob;
        public static void SetLastCompleted(BuildJob job) => _lastCompletedJob = job;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add MCPForUnity/Editor/Tools/Build/BuildJob.cs
git commit -m "feat: add BuildJob state machine and job store"
```

---

## Task 4: C# BuildSettingsHelper

Version-safe wrapper for PlayerSettings read/write. Always uses `NamedBuildTarget` (available since Unity 2021.2, our minimum supported version).

**Files:**
- Create: `MCPForUnity/Editor/Tools/Build/BuildSettingsHelper.cs`

- [ ] **Step 1: Create the settings helper**

```csharp
using System;
using UnityEditor;
using UnityEditor.Build;

namespace MCPForUnity.Editor.Tools.Build
{
    /// <summary>
    /// Version-safe wrappers for PlayerSettings. Always uses NamedBuildTarget
    /// (available since Unity 2021.2) to avoid deprecated BuildTargetGroup overloads.
    /// </summary>
    public static class BuildSettingsHelper
    {
        public static object ReadProperty(string property, NamedBuildTarget namedTarget)
        {
            switch (property.ToLowerInvariant())
            {
                case "product_name":
                    return new { property, value = PlayerSettings.productName };
                case "company_name":
                    return new { property, value = PlayerSettings.companyName };
                case "version":
                    return new { property, value = PlayerSettings.bundleVersion };
                case "bundle_id":
                    return new { property, value = PlayerSettings.GetApplicationIdentifier(namedTarget) };
                case "scripting_backend":
                    var backend = PlayerSettings.GetScriptingBackend(namedTarget);
                    return new { property, value = backend == ScriptingImplementation.IL2CPP ? "il2cpp" : "mono" };
                case "defines":
                    return new { property, value = PlayerSettings.GetScriptingDefineSymbols(namedTarget) };
                case "architecture":
                    var arch = PlayerSettings.GetArchitecture(namedTarget);
                    string archName = arch switch { 1 => "arm64", 2 => "universal", _ => "default" };
                    return new { property, value = archName, raw = arch };
                default:
                    return null; // Caller should return ErrorResponse
            }
        }

        public static string WriteProperty(string property, string value, NamedBuildTarget namedTarget)
        {
            try
            {
                switch (property.ToLowerInvariant())
                {
                    case "product_name":
                        PlayerSettings.productName = value;
                        return null;
                    case "company_name":
                        PlayerSettings.companyName = value;
                        return null;
                    case "version":
                        PlayerSettings.bundleVersion = value;
                        return null;
                    case "bundle_id":
                        PlayerSettings.SetApplicationIdentifier(namedTarget, value);
                        return null;
                    case "scripting_backend":
                        var impl = value.ToLowerInvariant() == "il2cpp"
                            ? ScriptingImplementation.IL2CPP
                            : ScriptingImplementation.Mono2x;
                        PlayerSettings.SetScriptingBackend(namedTarget, impl);
                        return null;
                    case "defines":
                        PlayerSettings.SetScriptingDefineSymbols(namedTarget, value);
                        return null;
                    case "architecture":
                        int arch = value.ToLowerInvariant() switch
                        {
                            "arm64" => 1,
                            "universal" => 2,
                            "x86_64" or "default" => 0,
                            _ => -1
                        };
                        if (arch < 0)
                            return $"Unknown architecture '{value}'. Valid: arm64, x86_64, universal";
                        PlayerSettings.SetArchitecture(namedTarget, arch);
                        return null;
                    default:
                        return $"Unknown property '{property}'. Valid: product_name, company_name, version, bundle_id, scripting_backend, defines, architecture";
                }
            }
            catch (Exception ex)
            {
                return $"Failed to set {property}: {ex.Message}";
            }
        }

        public static string[] ValidProperties => new[]
        {
            "product_name", "company_name", "version", "bundle_id",
            "scripting_backend", "defines", "architecture"
        };
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add MCPForUnity/Editor/Tools/Build/BuildSettingsHelper.cs
git commit -m "feat: add BuildSettingsHelper with version-safe PlayerSettings APIs"
```

---

## Task 5: C# BuildRunner (Async Build Execution)

Schedules builds via `EditorApplication.delayCall` and captures `BuildReport`.

**Files:**
- Create: `MCPForUnity/Editor/Tools/Build/BuildRunner.cs`

- [ ] **Step 1: Create the build runner**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Build
{
    public static class BuildRunner
    {
        /// <summary>
        /// Schedule a player build to run on the next editor update via delayCall.
        /// Returns immediately with a PendingResponse containing the job_id.
        /// </summary>
        public static object ScheduleBuild(BuildJob job, BuildPlayerOptions options)
        {
            job.State = BuildJobState.Pending;
            BuildJobStore.AddBuildJob(job);

            EditorApplication.delayCall += () => ExecuteBuild(job, options);

            return new PendingResponse(
                $"Build scheduled for {job.Target}. Polling for completion...",
                pollIntervalSeconds: 5.0,
                data: new { job_id = job.JobId, platform = job.Target.ToString() }
            );
        }

#if UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Schedule a profile-based build (Unity 6+ only).
        /// </summary>
        public static object ScheduleProfileBuild(BuildJob job, BuildPlayerWithProfileOptions options)
        {
            job.State = BuildJobState.Pending;
            BuildJobStore.AddBuildJob(job);

            EditorApplication.delayCall += () => ExecuteProfileBuild(job, options);

            return new PendingResponse(
                $"Profile build scheduled for {job.Target}. Polling for completion...",
                pollIntervalSeconds: 5.0,
                data: new { job_id = job.JobId, platform = job.Target.ToString() }
            );
        }
#endif

        /// <summary>
        /// Build a BuildPlayerOptions struct from parameters.
        /// </summary>
        public static BuildPlayerOptions CreateBuildOptions(
            BuildTarget target,
            string outputPath,
            string[] scenes,
            BuildOptions buildOptions,
            int subtarget)
        {
            var opts = new BuildPlayerOptions
            {
                target = target,
                targetGroup = BuildTargetMapping.GetTargetGroup(target),
                locationPathName = outputPath,
                scenes = scenes ?? GetDefaultScenes(),
                options = buildOptions,
                subtarget = subtarget
            };
            return opts;
        }

        /// <summary>
        /// Parse BuildOptions flags from string array.
        /// </summary>
        public static BuildOptions ParseBuildOptions(string[] optionNames, bool development)
        {
            var opts = BuildOptions.None;
            if (development)
                opts |= BuildOptions.Development;

            if (optionNames == null) return opts;

            foreach (var name in optionNames)
            {
                switch (name.ToLowerInvariant())
                {
                    case "clean_build": opts |= BuildOptions.CleanBuildCache; break;
                    case "auto_run": opts |= BuildOptions.AutoRunPlayer; break;
                    case "deep_profiling": opts |= BuildOptions.EnableDeepProfilingSupport; break;
                    case "compress_lz4": opts |= BuildOptions.CompressWithLz4; break;
                    case "strict_mode": opts |= BuildOptions.StrictMode; break;
                    case "detailed_report": opts |= BuildOptions.DetailedBuildReport; break;
                    case "allow_debugging": opts |= BuildOptions.AllowDebugging; break;
                    case "connect_profiler": opts |= BuildOptions.ConnectWithProfiler; break;
                    case "scripts_only": opts |= BuildOptions.BuildScriptsOnly; break;
                    case "show_player": opts |= BuildOptions.ShowBuiltPlayer; break;
                    case "include_tests": opts |= BuildOptions.IncludeTestAssemblies; break;
                }
            }
            return opts;
        }

        private static void ExecuteBuild(BuildJob job, BuildPlayerOptions options)
        {
            job.State = BuildJobState.Building;
            job.StartedAt = DateTime.UtcNow;

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                job.Report = report;
                job.CompletedAt = DateTime.UtcNow;

                if (report.summary.result == BuildResult.Succeeded)
                {
                    job.State = BuildJobState.Succeeded;
                }
                else
                {
                    job.State = BuildJobState.Failed;
#if UNITY_2022_3_OR_NEWER
                    job.ErrorMessage = report.SummarizeErrors();
#else
                    job.ErrorMessage = $"Build failed with result: {report.summary.result}";
#endif
                }
            }
            catch (Exception ex)
            {
                job.State = BuildJobState.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
            }

            BuildJobStore.SetLastCompleted(job);
        }

#if UNITY_6000_0_OR_NEWER
        private static void ExecuteProfileBuild(BuildJob job, BuildPlayerWithProfileOptions options)
        {
            job.State = BuildJobState.Building;
            job.StartedAt = DateTime.UtcNow;

            try
            {
                BuildReport report = BuildPipeline.BuildPlayer(options);
                job.Report = report;
                job.CompletedAt = DateTime.UtcNow;
                job.State = report.summary.result == BuildResult.Succeeded
                    ? BuildJobState.Succeeded
                    : BuildJobState.Failed;

                if (job.State == BuildJobState.Failed)
                    // SummarizeErrors() is safe here — Unity 6 (6000.0) implies 2022.3+
                    job.ErrorMessage = report.SummarizeErrors();
            }
            catch (Exception ex)
            {
                job.State = BuildJobState.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
            }

            BuildJobStore.SetLastCompleted(job);
        }
#endif

        /// <summary>
        /// Schedule the next build in a batch. Called after each child completes.
        /// </summary>
        public static void ScheduleNextBatchBuild(BatchJob batch, Func<int, BuildJob> createChildBuild)
        {
            batch.CurrentIndex++;

            if (batch.State == BuildJobState.Cancelled)
            {
                // Mark remaining children as skipped
                for (int i = batch.CurrentIndex; i < batch.Children.Count; i++)
                    batch.Children[i].State = BuildJobState.Skipped;
                return;
            }

            if (batch.CurrentIndex >= batch.Children.Count)
            {
                // All done
                bool anyFailed = batch.Children.Any(c => c.State == BuildJobState.Failed);
                batch.State = anyFailed ? BuildJobState.Failed : BuildJobState.Succeeded;
                return;
            }

            var child = batch.Children[batch.CurrentIndex];
            createChildBuild(batch.CurrentIndex);

            // After this child completes, schedule the next one
            EditorApplication.delayCall += () =>
            {
                void WaitForCompletion()
                {
                    if (child.State == BuildJobState.Building || child.State == BuildJobState.Pending)
                        return; // Still running

                    EditorApplication.update -= WaitForCompletion;
                    ScheduleNextBatchBuild(batch, createChildBuild);
                }
                EditorApplication.update += WaitForCompletion;
            };
        }

        private static string[] GetDefaultScenes()
        {
            return EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add MCPForUnity/Editor/Tools/Build/BuildRunner.cs
git commit -m "feat: add BuildRunner with async delayCall-based build execution"
```

---

## Task 6: C# ManageBuild HandleCommand

Main entry point — dispatches to helpers.

**Files:**
- Create: `MCPForUnity/Editor/Tools/ManageBuild.cs`

- [ ] **Step 1: Create ManageBuild.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools.Build;

namespace MCPForUnity.Editor.Tools
{
    [McpForUnityTool("manage_build", AutoRegister = false, Group = "core",
        RequiresPolling = true, PollAction = "status", MaxPollSeconds = 1800)]
    public static class ManageBuild
    {
        private static readonly string[] ValidActions =
            { "build", "status", "platform", "settings", "scenes", "profiles", "batch", "cancel" };

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);
            var actionResult = p.GetRequired("action");
            if (!actionResult.IsSuccess)
                return new ErrorResponse(actionResult.ErrorMessage);

            string action = actionResult.Value.ToLowerInvariant();

            if (!ValidActions.Contains(action))
                return new ErrorResponse(
                    $"Unknown action '{action}'. Valid actions: {string.Join(", ", ValidActions)}");

            try
            {
                switch (action)
                {
                    case "build": return HandleBuild(p);
                    case "status": return HandleStatus(p);
                    case "platform": return HandlePlatform(p);
                    case "settings": return HandleSettings(p);
                    case "scenes": return HandleScenes(p);
                    case "profiles": return HandleProfiles(p);
                    case "batch": return HandleBatch(p);
                    case "cancel": return HandleCancel(p);
                    default:
                        return new ErrorResponse($"Unknown action: '{action}'");
                }
            }
            catch (Exception ex)
            {
                return new ErrorResponse(ex.Message, new { stackTrace = ex.StackTrace });
            }
        }

        // ── build ──────────────────────────────────────────────────────

        private static object HandleBuild(ToolParams p)
        {
            if (BuildPipeline.isBuildingPlayer)
                return new ErrorResponse("A build is already in progress.");

            string targetName = p.Get("target");
            if (!BuildTargetMapping.TryResolveBuildTarget(targetName, out var target))
                return new ErrorResponse($"Unknown target '{targetName}'.");

            var group = BuildTargetMapping.GetTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return new ErrorResponse(
                    $"Platform '{target}' is not installed. Install it via Unity Hub.");

            string outputPath = p.Get("output_path")
                ?? BuildTargetMapping.GetDefaultOutputPath(target, PlayerSettings.productName);
            string[] scenes = p.GetStringArray("scenes");
            bool development = p.GetBool("development");
            string[] optionNames = p.GetStringArray("options");
            string subtargetStr = p.Get("subtarget");
            string scriptingBackend = p.Get("scripting_backend");

            // Apply scripting backend if specified (persistent change)
            if (!string.IsNullOrEmpty(scriptingBackend))
            {
                var namedTarget = BuildTargetMapping.GetNamedBuildTarget(target);
                var impl = scriptingBackend.ToLowerInvariant() == "il2cpp"
                    ? ScriptingImplementation.IL2CPP
                    : ScriptingImplementation.Mono2x;
                PlayerSettings.SetScriptingBackend(namedTarget, impl);
            }

#if UNITY_6000_0_OR_NEWER
            string profilePath = p.Get("profile");
            if (!string.IsNullOrEmpty(profilePath))
                return HandleProfileBuild(p, profilePath, outputPath, development, optionNames);
#else
            string profilePath = p.Get("profile");
            if (!string.IsNullOrEmpty(profilePath))
                McpLog.Warn($"Build Profile param ignored — requires Unity 6+. Current: {UnityEngine.Application.unityVersion}");
#endif

            var buildOptions = BuildRunner.ParseBuildOptions(optionNames, development);
            int subtarget = BuildTargetMapping.ResolveSubtarget(subtargetStr);
            var options = BuildRunner.CreateBuildOptions(target, outputPath, scenes, buildOptions, subtarget);

            string jobId = BuildJobStore.CreateJobId();
            var job = new BuildJob(jobId, target, outputPath);
            return BuildRunner.ScheduleBuild(job, options);
        }

#if UNITY_6000_0_OR_NEWER
        private static object HandleProfileBuild(ToolParams p, string profilePath, string outputPath,
            bool development, string[] optionNames)
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<
                UnityEditor.Build.Profile.BuildProfile>(profilePath);
            if (profile == null)
                return new ErrorResponse($"Build profile not found at: {profilePath}");

            var buildOptions = BuildRunner.ParseBuildOptions(optionNames, development);
            var options = new BuildPlayerWithProfileOptions
            {
                buildProfile = profile,
                locationPathName = outputPath,
                options = buildOptions
            };

            // BuildPlayerWithProfileOptions derives the actual target from the profile,
            // but we use activeBuildTarget for job metadata/status display
            var target = EditorUserBuildSettings.activeBuildTarget;
            string jobId = BuildJobStore.CreateJobId();
            var job = new BuildJob(jobId, target, outputPath);
            return BuildRunner.ScheduleProfileBuild(job, options);
        }
#endif

        // ── status ─────────────────────────────────────────────────────

        private static object HandleStatus(ToolParams p)
        {
            string jobId = p.Get("job_id");

            if (string.IsNullOrEmpty(jobId))
            {
                // Return last build report
                var last = BuildJobStore.LastCompletedJob;
                if (last == null)
                {
#if UNITY_6000_0_OR_NEWER
                    var latestReport = BuildReport.GetLatestReport();
                    if (latestReport != null)
                    {
                        var s = latestReport.summary;
                        return new SuccessResponse("Last build report from Unity.", new
                        {
                            result = s.result.ToString().ToLowerInvariant(),
                            platform = s.platform.ToString(),
                            output_path = s.outputPath,
                            total_size_mb = Math.Round(s.totalSize / (1024.0 * 1024.0), 2),
                            duration_seconds = s.totalTime.TotalSeconds,
                            errors = s.totalErrors,
                            warnings = s.totalWarnings
                        });
                    }
#endif
                    return new ErrorResponse("No build jobs found.");
                }
                return new SuccessResponse("Last completed build.", last.ToStatusResponse());
            }

            // Check batch jobs first
            var batchJob = BuildJobStore.GetBatchJob(jobId);
            if (batchJob != null)
                return new SuccessResponse($"Batch {batchJob.State}.", batchJob.ToStatusResponse());

            var buildJob = BuildJobStore.GetBuildJob(jobId);
            if (buildJob == null)
                return new ErrorResponse($"No job found with ID: {jobId}");

            if (buildJob.State == BuildJobState.Building || buildJob.State == BuildJobState.Pending)
            {
                return new PendingResponse(
                    $"Build {buildJob.State.ToString().ToLowerInvariant()}...",
                    pollIntervalSeconds: 5.0,
                    data: buildJob.ToStatusResponse()
                );
            }

            return new SuccessResponse($"Build {buildJob.State}.", buildJob.ToStatusResponse());
        }

        // ── platform ───────────────────────────────────────────────────

        private static object HandlePlatform(ToolParams p)
        {
            string targetName = p.Get("target");

            if (string.IsNullOrEmpty(targetName))
            {
                // Read current platform
                return new SuccessResponse("Current platform.", new
                {
                    target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    target_group = BuildTargetMapping.GetTargetGroup(
                        EditorUserBuildSettings.activeBuildTarget).ToString(),
                    subtarget = EditorUserBuildSettings.standaloneBuildSubtarget.ToString()
                });
            }

            // Switch platform
            if (!BuildTargetMapping.TryResolveBuildTarget(targetName, out var target))
                return new ErrorResponse($"Unknown target '{targetName}'.");

            var group = BuildTargetMapping.GetTargetGroup(target);
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
                return new ErrorResponse(
                    $"Platform '{target}' is not installed. Install it via Unity Hub.");

            if (EditorUserBuildSettings.activeBuildTarget == target)
                return new SuccessResponse("Already on this platform.", new
                {
                    target = target.ToString()
                });

            // Capture previous target before switching
            string previousTarget = EditorUserBuildSettings.activeBuildTarget.ToString();

            string subtargetStr = p.Get("subtarget");
            if (!string.IsNullOrEmpty(subtargetStr) && subtargetStr.ToLowerInvariant() == "server")
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;

            // SwitchActiveBuildTarget requires BuildTargetGroup (no NamedBuildTarget overload)
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

            return new PendingResponse(
                $"Switching to {target}. This reimports all assets and may take several minutes...",
                pollIntervalSeconds: 10.0,
                data: new { target = target.ToString(), previous = previousTarget }
            );
        }

        // ── settings ───────────────────────────────────────────────────

        private static object HandleSettings(ToolParams p)
        {
            var propertyResult = p.GetRequired("property");
            if (!propertyResult.IsSuccess)
                return new ErrorResponse(propertyResult.ErrorMessage);

            string property = propertyResult.Value.ToLowerInvariant();
            string targetName = p.Get("target");
            string value = p.Get("value");

            // Resolve target
            string err = BuildTargetMapping.TryResolveNamedBuildTarget(targetName, out var namedTarget);
            if (err != null)
                return new ErrorResponse(err);

            if (string.IsNullOrEmpty(value))
            {
                // Read
                var result = BuildSettingsHelper.ReadProperty(property, namedTarget);
                if (result == null)
                    return new ErrorResponse(
                        $"Unknown property '{property}'. Valid: {string.Join(", ", BuildSettingsHelper.ValidProperties)}");
                return new SuccessResponse($"Read {property}.", result);
            }

            // Write
            string writeErr = BuildSettingsHelper.WriteProperty(property, value, namedTarget);
            if (writeErr != null)
                return new ErrorResponse(writeErr);
            return new SuccessResponse($"Set {property} = {value}.",
                BuildSettingsHelper.ReadProperty(property, namedTarget));
        }

        // ── scenes ─────────────────────────────────────────────────────

        private static object HandleScenes(ToolParams p)
        {
            var scenesRaw = p.GetRaw("scenes");

            if (scenesRaw == null || scenesRaw.Type == JTokenType.Null)
            {
                // Read current scene list
                var scenes = EditorBuildSettings.scenes.Select(s => new
                {
                    path = s.path,
                    enabled = s.enabled,
                    guid = s.guid.ToString()
                }).ToArray();

                return new SuccessResponse($"Build scenes ({scenes.Length}).", new { scenes });
            }

            // Write scene list
            var sceneArray = scenesRaw as JArray;
            if (sceneArray == null)
                return new ErrorResponse("'scenes' must be an array of {path, enabled} objects.");

            var newScenes = new List<EditorBuildSettingsScene>();
            foreach (var item in sceneArray)
            {
                string path = item["path"]?.ToString();
                if (string.IsNullOrEmpty(path))
                    return new ErrorResponse("Each scene must have a 'path' field.");
                bool enabled = item["enabled"]?.Value<bool>() ?? true;
                newScenes.Add(new EditorBuildSettingsScene(path, enabled));
            }

            EditorBuildSettings.scenes = newScenes.ToArray();
            return new SuccessResponse($"Updated build scenes ({newScenes.Count}).", new
            {
                scenes = newScenes.Select(s => new { path = s.path, enabled = s.enabled }).ToArray()
            });
        }

        // ── profiles ───────────────────────────────────────────────────

        private static object HandleProfiles(ToolParams p)
        {
#if UNITY_6000_0_OR_NEWER
            string profilePath = p.Get("profile");
            bool activate = p.GetBool("activate");

            if (string.IsNullOrEmpty(profilePath))
            {
                // List all profiles
                var guids = AssetDatabase.FindAssets("t:BuildProfile");
                var profiles = guids.Select(guid =>
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var profile = AssetDatabase.LoadAssetAtPath<
                        UnityEditor.Build.Profile.BuildProfile>(path);
                    return new { path, name = System.IO.Path.GetFileNameWithoutExtension(path) };
                }).ToArray();

                var active = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
                return new SuccessResponse($"Found {profiles.Length} build profiles.", new
                {
                    profiles,
                    active_profile = active != null ? AssetDatabase.GetAssetPath(active) : null
                });
            }

            var loadedProfile = AssetDatabase.LoadAssetAtPath<
                UnityEditor.Build.Profile.BuildProfile>(profilePath);
            if (loadedProfile == null)
                return new ErrorResponse($"Build profile not found at: {profilePath}");

            if (activate)
            {
                UnityEditor.Build.Profile.BuildProfile.SetActiveBuildProfile(loadedProfile);
                return new SuccessResponse($"Activated build profile: {profilePath}", new
                {
                    profile = profilePath,
                    activated = true
                });
            }

            // Get profile details
            var profileScenes = loadedProfile.GetScenesForBuild()
                .Select(s => s.path).ToArray();
            return new SuccessResponse($"Profile: {profilePath}", new
            {
                profile = profilePath,
                scenes = profileScenes
            });
#else
            string version = UnityEngine.Application.unityVersion;
            return new ErrorResponse(
                $"Build Profiles require Unity 6 (6000.0+). Current version: {version}");
#endif
        }

        // ── batch ──────────────────────────────────────────────────────

        private static object HandleBatch(ToolParams p)
        {
            if (BuildPipeline.isBuildingPlayer)
                return new ErrorResponse("A build is already in progress.");

            string[] targets = p.GetStringArray("targets");
            string[] profiles = p.GetStringArray("profiles");
            string outputDir = p.Get("output_dir") ?? "Builds";
            bool development = p.GetBool("development");
            string[] optionNames = p.GetStringArray("options");

            if ((targets == null || targets.Length == 0) && (profiles == null || profiles.Length == 0))
                return new ErrorResponse("'targets' or 'profiles' is required for batch builds.");
            if (targets != null && targets.Length > 0 && profiles != null && profiles.Length > 0)
                return new ErrorResponse("Provide 'targets' or 'profiles', not both.");

            string batchId = BuildJobStore.CreateBatchId();
            var batch = new BatchJob(batchId);
            batch.State = BuildJobState.Building;
            BuildJobStore.AddBatchJob(batch);

            if (targets != null && targets.Length > 0)
            {
                // Tier 1: target-based batch
                foreach (var t in targets)
                {
                    if (!BuildTargetMapping.TryResolveBuildTarget(t, out var bt))
                        return new ErrorResponse($"Unknown target '{t}' in batch.");
                    string defaultPath = BuildTargetMapping.GetDefaultOutputPath(bt, PlayerSettings.productName);
                    // Replace default "Builds/" prefix with custom output dir
                    string path = defaultPath.StartsWith("Builds/")
                        ? $"{outputDir}/{defaultPath.Substring(7)}"
                        : $"{outputDir}/{defaultPath}";
                    var child = new BuildJob(BuildJobStore.CreateJobId(), bt, path);
                    batch.Children.Add(child);
                    BuildJobStore.AddBuildJob(child);
                }

                var buildOpts = BuildRunner.ParseBuildOptions(optionNames, development);

                BuildRunner.ScheduleNextBatchBuild(batch, index =>
                {
                    var child = batch.Children[index];
                    var group = BuildTargetMapping.GetTargetGroup(child.Target);

                    // Switch platform if needed
                    if (EditorUserBuildSettings.activeBuildTarget != child.Target)
                    {
                        var namedTarget = BuildTargetMapping.GetNamedBuildTarget(child.Target);
                        EditorUserBuildSettings.SwitchActiveBuildTarget(namedTarget, child.Target);
                    }

                    int subtarget = (int)StandaloneBuildSubtarget.Player;
                    var options = BuildRunner.CreateBuildOptions(
                        child.Target, child.OutputPath, null, buildOpts, subtarget);
                    BuildRunner.ScheduleBuild(child, options);
                    return child;
                });
            }
#if UNITY_6000_0_OR_NEWER
            else if (profiles != null && profiles.Length > 0)
            {
                // Tier 2: profile-based batch (Unity 6+)
                foreach (var profilePath in profiles)
                {
                    var profile = AssetDatabase.LoadAssetAtPath<
                        UnityEditor.Build.Profile.BuildProfile>(profilePath);
                    if (profile == null)
                        return new ErrorResponse($"Profile not found: {profilePath}");

                    var target = EditorUserBuildSettings.activeBuildTarget;
                    string name = System.IO.Path.GetFileNameWithoutExtension(profilePath);
                    string path = $"{outputDir}/{name}/{PlayerSettings.productName}";
                    var child = new BuildJob(BuildJobStore.CreateJobId(), target, path);
                    batch.Children.Add(child);
                    BuildJobStore.AddBuildJob(child);
                }

                var buildOpts = BuildRunner.ParseBuildOptions(optionNames, development);

                BuildRunner.ScheduleNextBatchBuild(batch, index =>
                {
                    var child = batch.Children[index];
                    string profilePath = profiles[index];
                    var profile = AssetDatabase.LoadAssetAtPath<
                        UnityEditor.Build.Profile.BuildProfile>(profilePath);

                    var opts = new BuildPlayerWithProfileOptions
                    {
                        buildProfile = profile,
                        locationPathName = child.OutputPath,
                        options = buildOpts
                    };
                    BuildRunner.ScheduleProfileBuild(child, opts);
                    return child;
                });
            }
#else
            else if (profiles != null && profiles.Length > 0)
            {
                return new ErrorResponse(
                    $"Profile-based batch requires Unity 6+. Current: {UnityEngine.Application.unityVersion}");
            }
#endif

            return new PendingResponse(
                $"Batch build started ({batch.Children.Count} builds).",
                pollIntervalSeconds: 10.0,
                data: new { job_id = batchId, total = batch.Children.Count }
            );
        }

        // ── cancel ─────────────────────────────────────────────────────

        private static object HandleCancel(ToolParams p)
        {
            var jobIdResult = p.GetRequired("job_id");
            if (!jobIdResult.IsSuccess)
                return new ErrorResponse(jobIdResult.ErrorMessage);

            string jobId = jobIdResult.Value;

            var batchJob = BuildJobStore.GetBatchJob(jobId);
            if (batchJob != null)
            {
                if (batchJob.State == BuildJobState.Building)
                {
                    batchJob.State = BuildJobState.Cancelled;
                    return new SuccessResponse(
                        "Batch cancelled. The current build will finish but no more builds will start.",
                        new { job_id = jobId, state = "cancelled" });
                }
                return new ErrorResponse($"Batch is already {batchJob.State}.");
            }

            var buildJob = BuildJobStore.GetBuildJob(jobId);
            if (buildJob != null)
            {
                if (buildJob.State == BuildJobState.Building)
                    return new ErrorResponse(
                        "Cannot cancel a single build in progress. BuildPipeline.BuildPlayer is " +
                        "synchronous and blocks the editor until completion.");
                return new ErrorResponse($"Build is already {buildJob.State}.");
            }

            return new ErrorResponse($"No job found with ID: {jobId}");
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add MCPForUnity/Editor/Tools/ManageBuild.cs
git commit -m "feat: add ManageBuild C# handler with all 8 actions"
```

---

## Task 7: Python MCP Tool

**Files:**
- Create: `Server/src/services/tools/manage_build.py`

- [ ] **Step 1: Create the Python tool**

```python
"""Build management — player builds, platform switching, settings, batch automation."""

from typing import Annotated, Any, Optional

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from services.tools.utils import coerce_bool, parse_json_payload
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

ALL_ACTIONS = [
    "build",
    "status",
    "platform",
    "settings",
    "scenes",
    "profiles",
    "batch",
    "cancel",
]


async def _send_build_command(
    ctx: Context,
    params_dict: dict[str, Any],
) -> dict[str, Any]:
    unity_instance = await get_unity_instance_from_context(ctx)
    result = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "manage_build", params_dict
    )
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}


@mcp_for_unity_tool(
    group="core",
    description=(
        "Manage Unity player builds — trigger builds, switch platforms, configure settings, "
        "manage build scenes and profiles, run batch builds across platforms. "
        "Actions: build, status, platform, settings, scenes, profiles, batch, cancel."
    ),
    annotations=ToolAnnotations(
        title="Manage Build",
        destructiveHint=True,
        readOnlyHint=False,
    ),
)
async def manage_build(
    ctx: Context,
    action: Annotated[str, "Action: build, status, platform, settings, scenes, profiles, batch, cancel"],
    target: Annotated[Optional[str], "Build target: windows64, osx, linux64, android, ios, webgl, uwp, tvos, visionos"] = None,
    output_path: Annotated[Optional[str], "Output path for the build"] = None,
    scenes: Annotated[Optional[str], "JSON array of scene paths, or comma-separated paths"] = None,
    development: Annotated[Optional[str], "Development build (true/false)"] = None,
    options: Annotated[Optional[str], "JSON array of BuildOptions: clean_build, auto_run, deep_profiling, compress_lz4, strict_mode, detailed_report"] = None,
    subtarget: Annotated[Optional[str], "Build subtarget: player or server"] = None,
    scripting_backend: Annotated[Optional[str], "Scripting backend: mono or il2cpp (persistent change)"] = None,
    profile: Annotated[Optional[str], "Build Profile asset path (Unity 6+ only)"] = None,
    property: Annotated[Optional[str], "Settings property: product_name, company_name, version, bundle_id, scripting_backend, defines, architecture"] = None,
    value: Annotated[Optional[str], "Value to set for the property (omit to read)"] = None,
    activate: Annotated[Optional[str], "Activate a build profile (true/false)"] = None,
    targets: Annotated[Optional[str], "JSON array of targets for batch build"] = None,
    profiles: Annotated[Optional[str], "JSON array of profile paths for batch build (Unity 6+)"] = None,
    output_dir: Annotated[Optional[str], "Base output directory for batch builds"] = None,
    job_id: Annotated[Optional[str], "Job ID for status/cancel"] = None,
) -> dict[str, Any]:
    action_lower = action.lower()
    if action_lower not in ALL_ACTIONS:
        return {
            "success": False,
            "message": f"Unknown action '{action}'. Valid actions: {', '.join(ALL_ACTIONS)}",
        }

    params_dict: dict[str, Any] = {"action": action_lower}

    # Coerce typed values
    coerced_development = coerce_bool(development, default=None)
    coerced_activate = coerce_bool(activate, default=None)
    parsed_scenes = parse_json_payload(scenes) if scenes else None
    parsed_options = parse_json_payload(options) if options else None
    parsed_targets = parse_json_payload(targets) if targets else None
    parsed_profiles = parse_json_payload(profiles) if profiles else None

    param_map: dict[str, Any] = {
        "target": target,
        "output_path": output_path,
        "scenes": parsed_scenes,
        "development": coerced_development,
        "options": parsed_options,
        "subtarget": subtarget,
        "scripting_backend": scripting_backend,
        "profile": profile,
        "property": property,
        "value": value,
        "activate": coerced_activate,
        "targets": parsed_targets,
        "profiles": parsed_profiles,
        "output_dir": output_dir,
        "job_id": job_id,
    }

    for key, val in param_map.items():
        if val is not None:
            params_dict[key] = val

    return await _send_build_command(ctx, params_dict)
```

- [ ] **Step 2: Commit**

```bash
git add Server/src/services/tools/manage_build.py
git commit -m "feat: add manage_build Python MCP tool"
```

---

## Task 8: Python Tests

**Files:**
- Create: `Server/tests/test_manage_build.py`

- [ ] **Step 1: Create test file**

```python
"""Tests for manage_build MCP tool."""

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from services.tools.manage_build import ALL_ACTIONS, manage_build


@pytest.fixture
def mock_unity(monkeypatch):
    """Patch Unity transport layer and return captured call dict."""
    captured: dict[str, object] = {}

    async def fake_send(send_fn, unity_instance, tool_name, params):
        captured["unity_instance"] = unity_instance
        captured["tool_name"] = tool_name
        captured["params"] = params
        return {"success": True, "message": "ok"}

    monkeypatch.setattr(
        "services.tools.manage_build.get_unity_instance_from_context",
        AsyncMock(return_value="unity-instance-1"),
    )
    monkeypatch.setattr(
        "services.tools.manage_build.send_with_unity_instance",
        fake_send,
    )
    return captured


# ── action validation ───────────────────────────────────────────────

def test_all_actions_count():
    assert len(ALL_ACTIONS) == 8


def test_unknown_action_returns_error(mock_unity):
    result = asyncio.run(manage_build(SimpleNamespace(), action="nonexistent"))
    assert result["success"] is False
    assert "Unknown action" in result["message"]
    assert "tool_name" not in mock_unity


# ── build action ────────────────────────────────────────────────────

def test_build_forwards_params(mock_unity):
    result = asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="build",
            target="windows64",
            development="true",
            output_path="Builds/Win/Game.exe",
            scripting_backend="il2cpp",
        )
    )
    assert result["success"] is True
    params = mock_unity["params"]
    assert params["action"] == "build"
    assert params["target"] == "windows64"
    assert params["development"] is True
    assert params["output_path"] == "Builds/Win/Game.exe"
    assert params["scripting_backend"] == "il2cpp"


def test_build_omits_none_params(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="build"))
    params = mock_unity["params"]
    assert params == {"action": "build"}


def test_build_with_options(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="build",
            options='["clean_build", "auto_run"]',
        )
    )
    params = mock_unity["params"]
    assert params["options"] == ["clean_build", "auto_run"]


def test_build_with_scenes(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="build",
            scenes='["Assets/Scenes/Main.unity", "Assets/Scenes/Level1.unity"]',
        )
    )
    params = mock_unity["params"]
    assert params["scenes"] == ["Assets/Scenes/Main.unity", "Assets/Scenes/Level1.unity"]


# ── status action ──────────────────────────────────────────────────

def test_status_forwards_job_id(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="status", job_id="build-abc123"))
    params = mock_unity["params"]
    assert params["action"] == "status"
    assert params["job_id"] == "build-abc123"


def test_status_without_job_id(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="status"))
    params = mock_unity["params"]
    assert params == {"action": "status"}


# ── platform action ────────────────────────────────────────────────

def test_platform_read(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="platform"))
    params = mock_unity["params"]
    assert params == {"action": "platform"}


def test_platform_switch(mock_unity):
    asyncio.run(
        manage_build(SimpleNamespace(), action="platform", target="android", subtarget="player")
    )
    params = mock_unity["params"]
    assert params["target"] == "android"
    assert params["subtarget"] == "player"


# ── settings action ────────────────────────────────────────────────

def test_settings_read(mock_unity):
    asyncio.run(
        manage_build(SimpleNamespace(), action="settings", property="product_name")
    )
    params = mock_unity["params"]
    assert params["action"] == "settings"
    assert params["property"] == "product_name"
    assert "value" not in params


def test_settings_write(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="settings",
            property="product_name",
            value="My Game",
        )
    )
    params = mock_unity["params"]
    assert params["property"] == "product_name"
    assert params["value"] == "My Game"


# ── scenes action ──────────────────────────────────────────────────

def test_scenes_read(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="scenes"))
    params = mock_unity["params"]
    assert params == {"action": "scenes"}


def test_scenes_write(mock_unity):
    scenes_json = '[{"path": "Assets/Scenes/Main.unity", "enabled": true}]'
    asyncio.run(manage_build(SimpleNamespace(), action="scenes", scenes=scenes_json))
    params = mock_unity["params"]
    assert params["scenes"] == [{"path": "Assets/Scenes/Main.unity", "enabled": True}]


# ── profiles action ────────────────────────────────────────────────

def test_profiles_list(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="profiles"))
    params = mock_unity["params"]
    assert params == {"action": "profiles"}


def test_profiles_activate(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="profiles",
            profile="Assets/Settings/Build Profiles/iOS.asset",
            activate="true",
        )
    )
    params = mock_unity["params"]
    assert params["profile"] == "Assets/Settings/Build Profiles/iOS.asset"
    assert params["activate"] is True


# ── batch action ────────────────────────────────────────────────────

def test_batch_with_targets(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="batch",
            targets='["windows64", "linux64", "webgl"]',
            development="true",
        )
    )
    params = mock_unity["params"]
    assert params["action"] == "batch"
    assert params["targets"] == ["windows64", "linux64", "webgl"]
    assert params["development"] is True


def test_batch_with_profiles(mock_unity):
    asyncio.run(
        manage_build(
            SimpleNamespace(),
            action="batch",
            profiles='["Assets/Profiles/A.asset", "Assets/Profiles/B.asset"]',
        )
    )
    params = mock_unity["params"]
    assert params["profiles"] == ["Assets/Profiles/A.asset", "Assets/Profiles/B.asset"]


# ── cancel action ──────────────────────────────────────────────────

def test_cancel_forwards_job_id(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="cancel", job_id="batch-xyz789"))
    params = mock_unity["params"]
    assert params["action"] == "cancel"
    assert params["job_id"] == "batch-xyz789"


# ── validation edge cases ───────────────────────────────────────────

def test_batch_requires_targets_or_profiles(mock_unity):
    """batch with neither targets nor profiles forwards to Unity (which validates)."""
    asyncio.run(manage_build(SimpleNamespace(), action="batch"))
    params = mock_unity["params"]
    assert params == {"action": "batch"}


def test_settings_requires_property(mock_unity):
    """settings without property forwards to Unity (which validates)."""
    asyncio.run(manage_build(SimpleNamespace(), action="settings"))
    params = mock_unity["params"]
    assert params == {"action": "settings"}


def test_cancel_requires_job_id(mock_unity):
    """cancel without job_id forwards to Unity (which validates)."""
    asyncio.run(manage_build(SimpleNamespace(), action="cancel"))
    params = mock_unity["params"]
    assert params == {"action": "cancel"}


# ── transport ───────────────────────────────────────────────────────

def test_sends_to_correct_tool_name(mock_unity):
    asyncio.run(manage_build(SimpleNamespace(), action="status"))
    assert mock_unity["tool_name"] == "manage_build"
```

- [ ] **Step 2: Run tests to verify they pass**

Run: `cd Server && uv run pytest tests/test_manage_build.py -v`

Expected: All tests PASS.

- [ ] **Step 3: Commit**

```bash
git add Server/tests/test_manage_build.py
git commit -m "test: add manage_build Python tool tests"
```

---

## Task 9: CLI Commands

**Files:**
- Create: `Server/src/cli/commands/build.py`
- Modify: `Server/src/cli/main.py`

- [ ] **Step 1: Create CLI command group**

```python
"""Build management CLI commands."""

import click
from typing import Optional

from cli.utils.config import get_config
from cli.utils.output import format_output, print_error, print_success, print_info
from cli.utils.connection import run_command, handle_unity_errors


@click.group()
def build():
    """Build management - player builds, platforms, settings, batch."""
    pass


@build.command("run")
@click.option("--target", "-t", help="Build target: windows64, osx, linux64, android, ios, webgl")
@click.option("--output", "-o", "output_path", help="Output path")
@click.option("--development", "-d", is_flag=True, help="Development build")
@click.option("--backend", "scripting_backend", type=click.Choice(["mono", "il2cpp"]), help="Scripting backend")
@click.option("--subtarget", type=click.Choice(["player", "server"]), help="Build subtarget")
@click.option("--profile", help="Build Profile asset path (Unity 6+)")
@click.option("--clean", is_flag=True, help="Clean build cache")
@click.option("--auto-run", is_flag=True, help="Auto-run after build")
@handle_unity_errors
def run_build(target, output_path, development, scripting_backend, subtarget, profile, clean, auto_run):
    """Trigger a player build.

    \b
    Examples:
        unity-mcp build run --target windows64 --development
        unity-mcp build run --target android --backend il2cpp
        unity-mcp build run --profile "Assets/Settings/Build Profiles/iOS.asset"
    """
    config = get_config()
    params = {"action": "build"}
    if target:
        params["target"] = target
    if output_path:
        params["output_path"] = output_path
    if development:
        params["development"] = True
    if scripting_backend:
        params["scripting_backend"] = scripting_backend
    if subtarget:
        params["subtarget"] = subtarget
    if profile:
        params["profile"] = profile

    options = []
    if clean:
        options.append("clean_build")
    if auto_run:
        options.append("auto_run")
    if options:
        params["options"] = options

    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        job_id = (result.get("data") or {}).get("job_id")
        if job_id:
            print_info(f"Build started. Poll with: unity-mcp build status {job_id}")


@build.command("status")
@click.argument("job_id", required=False)
@handle_unity_errors
def status(job_id: Optional[str]):
    """Check build status or get last build report.

    \b
    Examples:
        unity-mcp build status
        unity-mcp build status build-abc123
    """
    config = get_config()
    params = {"action": "status"}
    if job_id:
        params["job_id"] = job_id
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))


@build.command("platform")
@click.argument("target", required=False)
@click.option("--subtarget", type=click.Choice(["player", "server"]), help="Build subtarget")
@handle_unity_errors
def platform(target: Optional[str], subtarget: Optional[str]):
    """Read or switch the active build platform.

    \b
    Examples:
        unity-mcp build platform
        unity-mcp build platform android
        unity-mcp build platform windows64 --subtarget server
    """
    config = get_config()
    params = {"action": "platform"}
    if target:
        params["target"] = target
    if subtarget:
        params["subtarget"] = subtarget
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))


@build.command("settings")
@click.argument("property_name")
@click.option("--value", "-v", help="Value to set. Omit to read.")
@click.option("--target", "-t", help="Build target for platform-specific settings")
@handle_unity_errors
def settings(property_name: str, value: Optional[str], target: Optional[str]):
    """Read or write player settings.

    \b
    Properties: product_name, company_name, version, bundle_id,
                scripting_backend, defines, architecture

    \b
    Examples:
        unity-mcp build settings product_name
        unity-mcp build settings product_name --value "My Game"
        unity-mcp build settings scripting_backend --value il2cpp --target android
    """
    config = get_config()
    params = {"action": "settings", "property": property_name}
    if value:
        params["value"] = value
    if target:
        params["target"] = target
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))


@build.command("scenes")
@click.option("--set", "scene_paths", help="Comma-separated scene paths to set")
@handle_unity_errors
def scenes(scene_paths: Optional[str]):
    """Read or update the build scene list.

    \b
    Examples:
        unity-mcp build scenes
        unity-mcp build scenes --set "Assets/Scenes/Main.unity,Assets/Scenes/Level1.unity"
    """
    config = get_config()
    params = {"action": "scenes"}
    if scene_paths:
        scene_list = [
            {"path": p.strip(), "enabled": True}
            for p in scene_paths.split(",")
        ]
        params["scenes"] = scene_list
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))


@build.command("profiles")
@click.argument("profile", required=False)
@click.option("--activate", is_flag=True, help="Activate the specified profile")
@handle_unity_errors
def profiles_cmd(profile: Optional[str], activate: bool):
    """List, inspect, or activate build profiles (Unity 6+).

    \b
    Examples:
        unity-mcp build profiles
        unity-mcp build profiles "Assets/Settings/Build Profiles/iOS.asset"
        unity-mcp build profiles "Assets/Settings/Build Profiles/iOS.asset" --activate
    """
    config = get_config()
    params = {"action": "profiles"}
    if profile:
        params["profile"] = profile
    if activate:
        params["activate"] = True
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))


@build.command("batch")
@click.option("--targets", "-t", help="Comma-separated targets: windows64,linux64,webgl")
@click.option("--profiles", "-p", "profile_paths", help="Comma-separated profile paths (Unity 6+)")
@click.option("--output-dir", "-o", help="Base output directory")
@click.option("--development", "-d", is_flag=True, help="Development build for all")
@handle_unity_errors
def batch(targets, profile_paths, output_dir, development):
    """Run batch builds across multiple platforms or profiles.

    \b
    Examples:
        unity-mcp build batch --targets windows64,linux64,webgl
        unity-mcp build batch --profiles "Assets/Profiles/A.asset,Assets/Profiles/B.asset"
        unity-mcp build batch --targets windows64,android --development
    """
    config = get_config()
    params = {"action": "batch"}
    if targets:
        params["targets"] = [t.strip() for t in targets.split(",")]
    if profile_paths:
        params["profiles"] = [p.strip() for p in profile_paths.split(",")]
    if output_dir:
        params["output_dir"] = output_dir
    if development:
        params["development"] = True
    result = run_command("manage_build", params, config)
    click.echo(format_output(result, config.format))
    if result.get("success"):
        job_id = (result.get("data") or {}).get("job_id")
        if job_id:
            print_info(f"Batch started. Poll with: unity-mcp build status {job_id}")


@build.command("cancel")
@click.argument("job_id")
@handle_unity_errors
def cancel(job_id: str):
    """Cancel a build or batch job (best-effort).

    \b
    Examples:
        unity-mcp build cancel batch-xyz789
    """
    config = get_config()
    result = run_command("manage_build", {"action": "cancel", "job_id": job_id}, config)
    click.echo(format_output(result, config.format))
```

- [ ] **Step 2: Register the build CLI group**

In `Server/src/cli/main.py`, add to the `optional_commands` list (around line 272):

```python
("cli.commands.build", "build"),
```

- [ ] **Step 3: Commit**

```bash
git add Server/src/cli/commands/build.py Server/src/cli/main.py
git commit -m "feat: add build CLI commands"
```

---

## Task 10: Run All Python Tests

- [ ] **Step 1: Run the full test suite**

Run: `cd Server && uv run pytest tests/test_manage_build.py -v`

Expected: All tests PASS.

- [ ] **Step 2: Run the full project test suite to check for regressions**

Run: `cd Server && uv run pytest tests/ -v --timeout=30`

Expected: No regressions — all existing tests still pass.

- [ ] **Step 3: Commit (if any fixes were needed)**

---

## Task 11: Create Build Directory in Unity

Ensure the `MCPForUnity/Editor/Tools/Build/` directory exists and has an assembly definition reference if needed.

- [ ] **Step 1: Verify directory structure**

Run: `ls MCPForUnity/Editor/Tools/Build/ 2>/dev/null || mkdir -p MCPForUnity/Editor/Tools/Build/`

- [ ] **Step 2: Commit all C# files together**

```bash
git add MCPForUnity/Editor/Tools/Build/ MCPForUnity/Editor/Tools/ManageBuild.cs
git commit -m "feat: add ManageBuild C# implementation with async build execution"
```

---

## Task 12: Final Validation Checklist

- [ ] **Step 1: Verify Python tool auto-discovery**

Check that `manage_build` appears when listing tools:
```bash
cd Server && uv run python -c "from services.registry.tool_registry import _tool_registry; print([t['name'] for t in _tool_registry if 'build' in t['name']])"
```

- [ ] **Step 2: Verify C# compiles**

Open the Unity test project and check for compilation errors. The `ManageBuild.cs` and `Build/` files should compile without warnings (no deprecated API usage since we use `NamedBuildTarget` everywhere).

- [ ] **Step 3: Verify CLI registration**

```bash
cd Server && uv run unity-mcp build --help
```

Expected: Shows `build` group with subcommands: run, status, platform, settings, scenes, profiles, batch, cancel.

- [ ] **Step 4: Final commit if any fixes**

```bash
git add -A && git commit -m "fix: address validation issues from final checks"
```
