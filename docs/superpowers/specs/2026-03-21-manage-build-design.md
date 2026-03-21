# manage_build — Design Spec

**Date**: 2026-03-21
**Status**: Draft
**Scope**: Player builds, platform switching, build settings, batch automation

## Overview

`manage_build` is a single MCP tool (group: `core`) with 8 actions that gives AI assistants full control over Unity's player build pipeline. It consolidates build triggering, platform management, player settings, scene lists, build profiles (Unity 6+), and batch automation into one LLM-friendly interface.

No AssetBundle or Addressables builds — those are a separate domain for a future tool.

## Actions & Parameters

### `build`

Trigger a player build. Returns a `job_id` for async polling.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `target` | string | no | active platform | BuildTarget — `"windows64"`, `"osx"`, `"linux64"`, `"android"`, `"ios"`, `"webgl"`, etc. |
| `output_path` | string | no | `Builds/<target>/<productName>.<ext>` | Output path. Extension is platform-dependent: `.exe` (Windows), `.app` (macOS), `.x86_64` (Linux), `.apk`/`.aab` (Android), directory (iOS/WebGL) |
| `scenes` | string[] | no | EditorBuildSettings.scenes | Scene asset paths to include |
| `development` | bool | no | `false` | Development build (profiler, debugging) |
| `options` | string[] | no | `[]` | BuildOptions flags: `"clean_build"`, `"auto_run"`, `"deep_profiling"`, `"compress_lz4"`, `"strict_mode"`, `"detailed_report"` |
| `subtarget` | string | no | `"player"` | `"player"` or `"server"` |
| `scripting_backend` | string | no | current setting | `"mono"` or `"il2cpp"`. **Note**: This calls `PlayerSettings.SetScriptingBackend()` before building, which is a persistent change that triggers recompilation. The setting remains after the build completes. |
| `profile` | string | no | — | Build Profile asset path (Unity 6+ only; ignored on older versions with a warning) |

**Unity API**: [`BuildPipeline.BuildPlayer`](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html), [`BuildPlayerOptions`](https://docs.unity3d.com/ScriptReference/BuildPlayerOptions.html), [`BuildPlayerWithProfileOptions`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildPlayerWithProfileOptions.html)

### `status`

Poll a build job or retrieve the last build report.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `job_id` | string | no | — | Specific job ID. Omit to get last build report |

Returns: result (`succeeded`/`failed`/`in_progress`/`cancelled`), duration, total size, error/warning counts, output path.

**Unity API**: [`BuildReport`](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html), [`BuildSummary`](https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildSummary.html)

### `platform`

Read or switch the active build target.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `target` | string | no | — | Target to switch to. Omit to read current platform |
| `subtarget` | string | no | — | `"player"` or `"server"` |

When switching: returns `PendingResponse` (poll interval ~10s) since platform switch reimports all assets.

**Unity API**: [`EditorUserBuildSettings.activeBuildTarget`](https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings-activeBuildTarget.html), [`EditorUserBuildSettings.SwitchActiveBuildTarget`](https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html), [`BuildPipeline.IsBuildTargetSupported`](https://docs.unity3d.com/ScriptReference/BuildPipeline.IsBuildTargetSupported.html)

### `settings`

Read or write player settings.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `property` | string | yes | — | Setting name (see table below) |
| `value` | string | no | — | New value. Omit to read current |
| `target` | string | no | active platform | Uses the same friendly names as `build`/`platform` (e.g. `"windows64"`, `"android"`). C# maps to `NamedBuildTarget` internally. |

**Supported properties:**

| Property | Read/Write | Unity API |
|---|---|---|
| `product_name` | R/W | [`PlayerSettings.productName`](https://docs.unity3d.com/ScriptReference/PlayerSettings-productName.html) |
| `company_name` | R/W | [`PlayerSettings.companyName`](https://docs.unity3d.com/ScriptReference/PlayerSettings-companyName.html) |
| `version` | R/W | [`PlayerSettings.bundleVersion`](https://docs.unity3d.com/ScriptReference/PlayerSettings-bundleVersion.html) |
| `bundle_id` | R/W | [`PlayerSettings.SetApplicationIdentifier`](https://docs.unity3d.com/ScriptReference/PlayerSettings.SetApplicationIdentifier.html) |
| `scripting_backend` | R/W | [`PlayerSettings.SetScriptingBackend`](https://docs.unity3d.com/ScriptReference/PlayerSettings.SetScriptingBackend.html) |
| `architecture` | R/W | [`PlayerSettings.SetArchitecture`](https://docs.unity3d.com/ScriptReference/PlayerSettings.SetArchitecture.html). Accepts `"arm64"`, `"x86_64"`, `"universal"`. Availability varies by platform and Unity version. |
| `defines` | R/W | [`PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget)`](https://docs.unity3d.com/ScriptReference/PlayerSettings.GetScriptingDefineSymbols.html) (Unity 2021.2+) or `GetScriptingDefineSymbolsForGroup(BuildTargetGroup)` (older). C# selects the correct overload at runtime. Value is semicolon-separated string. |

### `scenes`

Read or replace the build scene list.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `scenes` | object[] | no | — | `[{"path": "Assets/Scenes/Main.unity", "enabled": true}]`. Omit to read current |

**Unity API**: [`EditorBuildSettings.scenes`](https://docs.unity3d.com/ScriptReference/EditorBuildSettings-scenes.html), [`EditorBuildSettingsScene`](https://docs.unity3d.com/ScriptReference/EditorBuildSettingsScene.html)

### `profiles`

List, inspect, or activate build profiles. Unity 6+ only — returns a clear error on older versions.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `profile` | string | no | — | Profile asset path to inspect or activate |
| `activate` | bool | no | `false` | If `true` with `profile`, activates it |

- Omit all params → list all available profiles
- `profile` only → get profile details (scenes, defines, target)
- `profile` + `activate=true` → activate that profile

**Unity API**: [`BuildProfile`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.html), [`BuildProfile.GetActiveBuildProfile`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.GetActiveBuildProfile.html), [`BuildProfile.SetActiveBuildProfile`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.SetActiveBuildProfile.html)

### `batch`

Build multiple platforms or profiles sequentially. Returns a batch `job_id`.

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `targets` | string[] | no | — | List of BuildTargets. Tier 1 — all Unity versions |
| `profiles` | string[] | no | — | List of Build Profile asset paths. Tier 2 — Unity 6+ only |
| `output_dir` | string | no | `"Builds/"` | Base output directory (subfolders per target/profile) |
| `development` | bool | no | `false` | Development build for all |
| `options` | string[] | no | `[]` | BuildOptions applied to all builds |

One of `targets` or `profiles` is required (not both). Each sub-build gets its own status in the batch report. Platform switches happen automatically between target-based builds.

### `cancel`

Cancel a build or batch (best-effort).

| Param | Type | Required | Default | Description |
|---|---|---|---|---|
| `job_id` | string | yes | — | Build or batch job ID |

**Behavior by job type**:
- **Batch job**: Prevents the next queued build from starting. The currently running build completes normally.
- **Single build job**: Returns a warning — `BuildPlayer` is synchronous on the main thread and **cannot** be aborted mid-compilation. The build runs to completion regardless.

## C# Architecture

### Tool Registration

```csharp
[McpForUnityTool("manage_build", AutoRegister = false, Group = "core",
    RequiresPolling = true, PollAction = "status")]
public static class ManageBuild { ... }
```

`RequiresPolling = true` and `PollAction = "status"` are required so the Python-side polling middleware (`custom_tool_service.py`) automatically handles status polling for `PendingResponse` results.

### File Structure

```
MCPForUnity/Editor/Tools/ManageBuild.cs      — HandleCommand + action dispatch
MCPForUnity/Editor/Tools/Build/BuildJob.cs    — Job state machine + storage
MCPForUnity/Editor/Tools/Build/BuildRunner.cs — delayCall-based async execution
```

### BuildTarget → BuildTargetGroup Mapping

`SwitchActiveBuildTarget` and `BuildPlayerOptions` require both `BuildTarget` and `BuildTargetGroup`. The C# implementation must include an internal mapping:

```csharp
static BuildTargetGroup GetTargetGroup(BuildTarget target) => target switch
{
    BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64
        or BuildTarget.StandaloneOSX or BuildTarget.StandaloneLinux64
        => BuildTargetGroup.Standalone,
    BuildTarget.iOS => BuildTargetGroup.iOS,
    BuildTarget.Android => BuildTargetGroup.Android,
    BuildTarget.WebGL => BuildTargetGroup.WebGL,
    BuildTarget.WSAPlayer => BuildTargetGroup.WSAPlayer,
    BuildTarget.tvOS => BuildTargetGroup.tvOS,
    // ... etc
    _ => BuildTargetGroup.Unknown,
};
```

Similarly, friendly target names (from Python) are resolved to `BuildTarget` enum values, and platform-specific settings APIs receive a `NamedBuildTarget` derived from this mapping.

### Async Build Execution

`BuildPipeline.BuildPlayer` blocks the editor thread. Unity Editor APIs must run on the main thread — no background threads. The pattern:

```
MCP Request (action=build)
  → HandleCommand
  → Create BuildJob (state: PENDING)
  → Schedule via EditorApplication.delayCall
  → Return PendingResponse(message, pollIntervalSeconds=5, data: {job_id, platform, ...})

EditorApplication.delayCall fires:
  → Set BuildJob state: BUILDING
  → BuildPipeline.BuildPlayer(options) → BuildReport
  → Set BuildJob state: SUCCEEDED or FAILED
  → Store serialized BuildReport on job

MCP Request (action=status, job_id)
  → HandleCommand
  → Read BuildJob
  → Return SuccessResponse with report data
```

### BuildJob State Machine

```
Single build:  PENDING → BUILDING → SUCCEEDED / FAILED
Batch parent:  PENDING → BUILDING → SUCCEEDED / FAILED / CANCELLED
Batch child:   PENDING → BUILDING → SUCCEEDED / FAILED / SKIPPED
```

`CANCELLED` only applies to batch parents (via `cancel` action). `SKIPPED` marks batch children that were not started due to cancellation. Single builds cannot be cancelled — `cancel` on a single build returns a warning.

For batch jobs, a parent `BatchJob` holds an ordered list of child `BuildJob` references. After each child completes, the next is scheduled via another `EditorApplication.delayCall`. Setting `cancel` on the batch prevents the next child from being scheduled.

### Version Detection

```csharp
static bool HasBuildProfiles => Type.GetType(
    "UnityEditor.Build.Profile.BuildProfile, UnityEditor") != null;
```

When `false`:
- `profiles` action → error with version message
- `batch` with `profiles` param → same error
- `build` with `profile` param → ignored with warning in response

### Platform Validation

Before any build or platform switch:
```csharp
if (!BuildPipeline.IsBuildTargetSupported(targetGroup, buildTarget))
    return new ErrorResponse("Platform not installed. Install via Unity Hub.");
```

### Concurrent Build Rejection

```csharp
if (BuildPipeline.isBuildingPlayer)
    return new ErrorResponse("A build is already in progress (job_id: ...).");
```

### BuildReport Serialization

**Single build status response:**

```json
{
  "success": true,
  "job_id": "build-abc123",
  "result": "succeeded",
  "platform": "StandaloneWindows64",
  "output_path": "Builds/Win/Game.exe",
  "total_size_mb": 142.5,
  "duration_seconds": 87.3,
  "errors": 0,
  "warnings": 3,
  "warning_messages": ["...", "...", "..."]
}
```

**Batch status response:**

```json
{
  "success": true,
  "job_id": "batch-xyz789",
  "result": "in_progress",
  "completed": 1,
  "total": 3,
  "current_build": "build-abc124",
  "builds": [
    {"job_id": "build-abc123", "platform": "StandaloneWindows64", "result": "succeeded", "duration_seconds": 87.3},
    {"job_id": "build-abc124", "platform": "Android", "result": "in_progress"},
    {"job_id": "build-abc125", "platform": "WebGL", "result": "pending"}
  ]
}
```

## Python MCP Tool

### File

`Server/src/services/tools/manage_build.py`

Single `manage_build` function with `@mcp_for_unity_tool(group="core")`. Validates action against the 8 valid actions, builds `params_dict`, sends to Unity via `send_with_unity_instance`.

### Target Name Mapping

Python accepts friendly names and maps to Unity enum values:

| Friendly Name | BuildTarget |
|---|---|
| `windows64` | `StandaloneWindows64` |
| `windows` | `StandaloneWindows` |
| `osx` | `StandaloneOSX` |
| `linux64` | `StandaloneLinux64` |
| `android` | `Android` |
| `ios` | `iOS` |
| `webgl` | `WebGL` |
| `uwp` | `WSAPlayer` |
| `tvos` | `tvOS` |
| `visionos` | `VisionOS` |

Mapping happens in C# — Python passes the friendly string through.

## CLI Commands

### File

`Server/src/cli/commands/build.py`

Click group `build` with subcommands mirroring each action:

```
unity-mcp build run --target windows64 --development
unity-mcp build status [JOB_ID]
unity-mcp build platform [TARGET]
unity-mcp build settings PROPERTY [--value VALUE]
unity-mcp build scenes [--set path1,path2]
unity-mcp build profiles [PROFILE] [--activate]
unity-mcp build batch --targets windows64,linux64,webgl
unity-mcp build cancel JOB_ID
```

## Limitations

### Cannot Cancel Mid-Build
`BuildPipeline.BuildPlayer` is synchronous. Once started, it runs to completion. `cancel` only prevents the next build in a batch queue from starting. The response communicates this clearly.

### Platform Switching is Slow
[`SwitchActiveBuildTarget`](https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html) reimports all assets for the new platform. This can take minutes on large projects. The `platform` action and `batch` with `targets` warn about this.

### Editor Blocked During Build — Polling Timeout Handling

While `BuildPlayer` runs, the editor is unresponsive. WebSocket messages queue but can't be processed. The `PendingResponse` is sent *before* the build starts (via `delayCall`), so the client knows to wait.

**Critical**: The Python-side polling middleware (`custom_tool_service.py`) has a `_MAX_POLL_SECONDS` deadline. If a build takes longer than this, the poll chain would normally timeout and return an error — even though the build is still running successfully in Unity.

**Solution**: The Python `manage_build` tool must handle transport-level timeouts differently from status-level failures during `build` and `batch` actions:
- When a `status` poll receives a **transport timeout** (WebSocket timeout, connection error) for a job in `BUILDING` state, treat it as "still building" and **continue polling** rather than failing. The existing `_poll_until_complete` in `custom_tool_service.py` already converts transport exceptions into synthetic `pending` responses — this part works out of the box.
- The real gap is `_MAX_POLL_SECONDS` (currently 600s / 10 minutes). Builds can exceed this. **Mechanism**: Add an optional `MaxPollSeconds` property to `McpForUnityToolAttribute` (default: `None`, meaning use `_MAX_POLL_SECONDS`). `manage_build` sets `MaxPollSeconds = 1800` (30 minutes). `_poll_until_complete` reads this from the tool's registration metadata and uses it as the deadline instead of the hardcoded default.
- Only treat an explicit `{"success": false}` response from Unity as a build failure.
- After the build completes and the editor becomes responsive again, the next `status` poll will succeed and return the `BuildReport`.

This is similar to how `refresh_unity.py` handles connection-lost scenarios during domain reload — it distinguishes transport failures from command failures.

### No Parallel Builds
Only one build can run at a time ([`BuildPipeline.isBuildingPlayer`](https://docs.unity3d.com/ScriptReference/BuildPipeline-isBuildingPlayer.html)). Concurrent requests are rejected with the active `job_id`.

### Console Platforms Require SDKs
PS4, PS5, Xbox, Switch targets require proprietary SDKs not included with Unity. `IsBuildTargetSupported` catches this before any work begins.

### Build Profiles Unity 6+ Only
[`BuildProfile`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.html) and [`BuildPlayerWithProfileOptions`](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildPlayerWithProfileOptions.html) are only available in Unity 6 (6000.0+). Detected at runtime via reflection. Older versions get clear error messages, no crashes.

## Testing Strategy

### Python Tests (`Server/tests/test_manage_build.py`)

- Unknown action returns error with valid action list
- Parameter validation: `settings` without `property`, `cancel` without `job_id`, `batch` without `targets`/`profiles`
- `batch` rejects both `targets` and `profiles` simultaneously
- Each action correctly builds `params_dict` forwarded to Unity
- `coerce_bool` works for `development` param
- `profile` param included when provided
- Transport timeout during `BUILDING` state continues polling instead of failing

### C# Tests (`TestProjects/UnityMCPTests/`)

- BuildJob state machine: PENDING → BUILDING → SUCCEEDED/FAILED
- Cancel semantics on batch jobs
- Platform validation: `IsBuildTargetSupported` rejection
- Version detection: `HasBuildProfiles` gating
- Report serialization: `BuildReport` → JSON shape
- Concurrent rejection: second request while `isBuildingPlayer`
- Batch sequencing: child jobs execute in order, cancel stops queue

### CLI Tests

Click test runner verifies commands map to correct tool actions and param dicts.

## Unity API Reference

| API | Documentation |
|---|---|
| `BuildPipeline` | https://docs.unity3d.com/ScriptReference/BuildPipeline.html |
| `BuildPipeline.BuildPlayer` | https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildPlayer.html |
| `BuildPlayerOptions` | https://docs.unity3d.com/ScriptReference/BuildPlayerOptions.html |
| `BuildPlayerWithProfileOptions` | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/BuildPlayerWithProfileOptions.html |
| `BuildTarget` | https://docs.unity3d.com/ScriptReference/BuildTarget.html |
| `BuildOptions` | https://docs.unity3d.com/ScriptReference/BuildOptions.html |
| `BuildReport` | https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildReport.html |
| `BuildSummary` | https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildSummary.html |
| `BuildResult` | https://docs.unity3d.com/ScriptReference/Build.Reporting.BuildResult.html |
| `EditorBuildSettings` | https://docs.unity3d.com/ScriptReference/EditorBuildSettings.html |
| `EditorBuildSettingsScene` | https://docs.unity3d.com/ScriptReference/EditorBuildSettingsScene.html |
| `EditorUserBuildSettings` | https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.html |
| `SwitchActiveBuildTarget` | https://docs.unity3d.com/ScriptReference/EditorUserBuildSettings.SwitchActiveBuildTarget.html |
| `IsBuildTargetSupported` | https://docs.unity3d.com/ScriptReference/BuildPipeline.IsBuildTargetSupported.html |
| `isBuildingPlayer` | https://docs.unity3d.com/ScriptReference/BuildPipeline-isBuildingPlayer.html |
| `PlayerSettings` | https://docs.unity3d.com/ScriptReference/PlayerSettings.html |
| `PlayerSettings.SetScriptingBackend` | https://docs.unity3d.com/ScriptReference/PlayerSettings.SetScriptingBackend.html |
| `PlayerSettings.SetArchitecture` | https://docs.unity3d.com/ScriptReference/PlayerSettings.SetArchitecture.html |
| `PlayerSettings.SetApplicationIdentifier` | https://docs.unity3d.com/ScriptReference/PlayerSettings.SetApplicationIdentifier.html |
| `ScriptingImplementation` | https://docs.unity3d.com/ScriptReference/ScriptingImplementation.html |
| `NamedBuildTarget` | https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.NamedBuildTarget.html |
| `StandaloneBuildSubtarget` | https://docs.unity3d.com/ScriptReference/StandaloneBuildSubtarget.html |
| `BuildProfile` (Unity 6+) | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.html |
| `BuildProfile.GetActiveBuildProfile` | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.GetActiveBuildProfile.html |
| `BuildProfile.SetActiveBuildProfile` | https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Build.Profile.BuildProfile.SetActiveBuildProfile.html |
| Build Profiles Manual (Unity 6) | https://docs.unity3d.com/6000.0/Documentation/Manual/build-profiles.html |
