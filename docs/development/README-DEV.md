# MCP for Unity - Developer Guide

| [English](README-DEV.md) | [简体中文](README-DEV-zh.md) |
|---------------------------|------------------------------|

## Contributing

**Branch off `beta`** to create PRs. The `main` branch is reserved for stable releases.

Before proposing major new features, please reach out to discuss - someone may already be working on it or it may have been considered previously. Open an issue or discussion to coordinate.

## Local Development Setup

### 1. Point Unity to Your Local Server

For the fastest iteration when working on the Python server:

1. Open Unity and go to **Window > MCP for Unity**
2. Open **Settings > Advanced Settings**
3. Set **Server Source Override** to your local `Server/` directory path
4. Enable **Dev Mode (Force fresh server install)** - this adds `--refresh` to uvx commands so your changes are picked up on every server start

### 2. Switch Package Sources

You may want to use the `mcp_source.py` script to quickly switch your Unity project between different MCP package sources [allows you to quickly point your personal project to your local or remote unity-mcp repo, or the live upstream (Coplay) versions of the unity-mcp package]:

```bash
python mcp_source.py
```

Options:
1. **Upstream main** - stable release (CoplayDev/unity-mcp)
2. **Upstream beta** - development branch (CoplayDev/unity-mcp#beta)
3. **Remote branch** - your fork's current branch
4. **Local workspace** - file: URL to your local MCPForUnity folder

After switching, open Package Manager in Unity and Refresh to re-resolve packages.

## Tool Selection & the Meta-Tool

MCP for Unity organizes tools into **groups** (Core, VFX & Shaders, Animation, UI Toolkit, Scripting Extensions, Testing). You can selectively enable or disable tools to control which capabilities are exposed to AI clients — reducing context window usage and focusing the AI on relevant tools.

### Using the Tools Tab in the Editor

Open **Window > MCP for Unity** and switch to the **Tools** tab. Each tool group is displayed as a collapsible foldout with:

- **Per-tool toggles** — click individual tool toggles to enable or disable them.
- **Group checkbox** — a checkbox embedded directly in each group's foldout header (next to the group title) enables or disables all tools in that group at once without expanding or collapsing the foldout.
- **Enable All / Disable All** — global buttons to toggle all tools.
- **Rescan** — re-discovers tools from assemblies (useful after adding new `[McpForUnityTool]` classes).
- **Reconfigure Clients** — re-registers tools with the server and reconfigures all detected MCP clients in one click, applying your changes without navigating back to the Clients tab.

### How Changes Propagate

Tool visibility changes work differently depending on the transport mode:

**HTTP mode** (recommended):

1. Toggling a tool calls `ReregisterToolsAsync()`, which sends the updated enabled tool list to the Python server over WebSocket.
2. The server updates its internal tool visibility via `mcp.enable()`/`mcp.disable()` per group.
3. The server sends a `tools/list_changed` MCP notification to all connected client sessions.
4. Already-connected clients (Claude Desktop, VS Code, etc.) automatically receive the updated tool list.

**Stdio mode**:

1. Toggles are persisted locally but cannot be pushed to the server (no WebSocket connection).
2. The server starts with all groups enabled. After changing toggles, ask the AI to run `manage_tools` with `action='sync'` — this pulls the current tool states from Unity and syncs server visibility.
3. Alternatively, restart the server to pick up changes.

### The `manage_tools` Meta-Tool

The server exposes a built-in `manage_tools` tool (always visible, not group-gated) that AIs can call directly:

| Action | Description |
|--------|-------------|
| `list_groups` | Lists all tool groups with their tools and enable/disable status |
| `activate` | Enables a tool group by name (e.g., `group="vfx"`) |
| `deactivate` | Disables a tool group by name |
| `sync` | Pulls current tool states from Unity and syncs server visibility (essential for stdio mode) |
| `reset` | Restores default tool visibility |

### When You Need to Reconfigure

After toggling tools on/off, MCP clients need to learn about the changes:

- **HTTP mode**: Changes propagate automatically via `tools/list_changed`. Most clients pick this up immediately. If a client doesn't, click **Reconfigure Clients** on the Tools tab, or go to Clients tab and click Configure.
- **Stdio mode**: The server process needs to be told about changes. Either ask the AI to call `manage_tools(action='sync')`, or restart the MCP session. Click **Reconfigure Clients** to re-register all clients with updated config.

## Running Tests

All major new features (and some minor ones) must include test coverage. It's so easy to get LLMs to write tests, ya gotta do it!

### Python Tests 

Located in `Server/tests/`:

```bash
cd Server
uv run pytest tests/ -v
```

### Unity C# Tests

Located in `TestProjects/UnityMCPTests/Assets/Tests/`.

**Using the CLI** (requires Unity running with MCP bridge connected):

```bash
cd Server

# Run EditMode tests (default)
uv run python -m cli.main editor tests

# Run PlayMode tests
uv run python -m cli.main editor tests --mode PlayMode

# Run async and poll for results (useful for long test runs)
uv run python -m cli.main editor tests --async
uv run python -m cli.main editor poll-test <job_id> --wait 60

# Show only failed tests
uv run python -m cli.main editor tests --failed-only
```

**Using MCP tools directly** (from any MCP client):

```
run_tests(mode="EditMode")
run_tests(mode="PlayMode", init_timeout=120000)  # PlayMode may need longer init due to domain reload
get_test_job(job_id="<id>", wait_timeout=60)
```

### Code Coverage

```bash
cd Server
uv run pytest tests/ --cov --cov-report=html
open htmlcov/index.html
```

## Unity-version CI matrix

CI exercises the package across multiple Unity versions to catch breaks in `#if UNITY_*_OR_NEWER` branches. The matrix is configured in `tools/unity-versions.json` and consumed by `.github/workflows/unity-tests.yml`.

**Every PR gets a unity-tests status check on open** (mirrors `python-tests.yml`). For same-repo PRs the default Unity 6 leg actually runs; for fork PRs the workflow appears but skips with a "missing license secrets" notice until a maintainer applies `safe-to-test` (existing secret-safety gate). The full 4-version matrix is opt-in via the `full-matrix` label.

**When the full matrix runs (all 4 versions in parallel):**

- Push to `beta` (the release gate).
- `workflow_call` from `beta-release.yml` / `release.yml`.
- Manual `workflow_dispatch` from the Actions tab.
- Any PR (in-repo or fork) labeled with **`full-matrix`** — apply when your change touches compat shims, conditional compilation, or anything else version-sensitive. Triggers a full-matrix run on the next `pull_request` or `pull_request_target` event. Cost is ~6-8 min wall clock vs ~3 min for the default leg.

Fork PRs still need `safe-to-test` as the base gate (so secrets are exposed against reviewed-only fork code); `full-matrix` is layered on top for fork-PR full-matrix runs.

**Default (single leg)** — every other path runs only against `defaultVersion` from `tools/unity-versions.json` (currently Unity 6.0 LTS, `6000.0.75f1`). The `floor` role (`2021.3.45f2`) still identifies the package minimum and runs as part of the full matrix; it's no longer the default-leg version.

## Local Unity-version parity check

The same `tools/unity-versions.json` drives a local script so you can reproduce CI behavior before pushing.

The script has two runners. Use whichever fits your setup:

| Runner | When to use | Cost |
|---|---|---|
| **Local Unity Hub** (default) | You already have one or more matrix versions installed via Unity Hub. Fastest. | Disk: each editor is 3-6 GB. |
| **GameCI Docker** (`--docker`) | You don't want to install every editor locally. Same containers CI uses. | One-time pull is 5-15 GB per version. On Apple Silicon, expect ~5-10× slowdown from amd64 emulation. |

### Local Unity Hub mode

```bash
# Compile-only check across every locally-installed Unity in the matrix (~30-60s warm per version).
tools/check-unity-versions.sh

# Full EditMode test run — matches what CI runs end-to-end.
tools/check-unity-versions.sh --full

# Filter to one version family.
tools/check-unity-versions.sh --only 6000.0
```

Windows uses the PowerShell companion:

```powershell
pwsh .\tools\check-unity-versions.ps1
pwsh .\tools\check-unity-versions.ps1 -Full
pwsh .\tools\check-unity-versions.ps1 -Only 6000.0
```

Versions not installed via Unity Hub are skipped — the script never forces you to install every editor in the matrix. Install the versions you most care about (typically the floor `2021.3.45f2` and your daily-driver Unity 6) and CI / Docker mode cover the rest.

### GameCI Docker mode (no Unity Hub install required)

```bash
tools/check-unity-versions.sh --docker                # all matrix versions, compile-only
tools/check-unity-versions.sh --docker --full         # full EditMode run
tools/check-unity-versions.sh --docker --only 2022.3  # one version family
```

```powershell
pwsh .\tools\check-unity-versions.ps1 -Docker
pwsh .\tools\check-unity-versions.ps1 -Docker -Full
```

**One-time setup: get a Unity license**

GameCI containers still need an activated Unity license. Free Personal activations work fine and are tied to the machine, not the editor version — so a single `.ulf` covers every version in the matrix.

```bash
# 1. Generate the request file (.alf) — outputs Unity_v<version>.alf in the current directory.
docker run --rm -v "$PWD":/work unityci/editor:ubuntu-2021.3.45f2-base-3 \
  /opt/unity/Editor/Unity -batchmode -nographics -quit \
  -createManualActivationFile -logFile /dev/stdout

# 2. Upload Unity_v<version>.alf at https://license.unity3d.com/manual
#    Choose Personal license → save the resulting .ulf file.

# 3. Export the .ulf contents (add to ~/.zshrc or ~/.bashrc to persist):
export UNITY_LICENSE="$(cat /path/to/Unity_v<version>.ulf)"

# 4. Run the check.
tools/check-unity-versions.sh --docker
```

PowerShell equivalent for step 3: `$env:UNITY_LICENSE = Get-Content C:\path\to\Unity_v<version>.ulf -Raw`.

The same `UNITY_LICENSE` secret is what `.github/workflows/unity-tests.yml` uses in CI — once you have it, your local Docker runs and CI behave identically.

**Coverage gap on Apple Silicon Macs**: GameCI publishes only `linux/amd64` images. Docker Desktop runs them under Rosetta/QEMU at ~5-10× the native amd64 speed. A compile that takes 30s on Intel takes 3-5 min on M-series. Still faster than installing four Unity editors, but plan for it.

**Opt-in pre-push hook**

```bash
tools/install-hooks.sh             # installs .git/hooks/pre-push (idempotent)
tools/install-hooks.sh --uninstall # removes our hooks
```

Once installed, `git push` runs the compile-only check first when the push touches `MCPForUnity/Editor/**`, `MCPForUnity/Runtime/**`, `TestProjects/UnityMCPTests/**`, `tools/unity-versions.json`, or `.github/workflows/unity-tests.yml`. Pushes that touch only docs, Server/, or unrelated files skip the check.

To bypass for a single push: `git push --no-verify`.

**Bumping the matrix**

Edit `tools/unity-versions.json` and update CI + local scripts both in one commit. The file is the single source of truth.

