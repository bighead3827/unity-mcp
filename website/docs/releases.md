---
id: releases
slug: /releases
title: Release Notes
sidebar_label: Releases
description: Full version-by-version change history for MCP for Unity.
---

# Release Notes

Latest releases land in [`beta`](https://github.com/CoplayDev/unity-mcp/tree/beta) before promotion to [`main`](https://github.com/CoplayDev/unity-mcp/tree/main). Major breaking changes get a dedicated migration guide under [Migrations](/migrations/v5).

For the canonical changelog with PR links, see [GitHub Releases](https://github.com/CoplayDev/unity-mcp/releases).

## v9.6 series

- **v9.6.3 (beta)** — New `manage_profiler` tool (14 actions): Profiler session control (start/stop/status/set areas), frame timing and counter reads, object memory queries, memory snapshots (take/list/compare via `com.unity.memoryprofiler`), and Frame Debugger (enable/disable/get events). Group: `profiling`.
- **v9.6.2** — New `manage_physics` tool (21 actions): physics settings, layer collision matrix, physics materials, joints (5 3D + 9 2D types), queries (raycast, raycast_all, linecast, shapecast, overlap), force application (`AddForce`/`AddTorque`/`AddExplosionForce`), rigidbody configuration, scene-wide validation, and edit-mode simulation. Full 3D and 2D support.
- **v9.6.1** — QoL extensions: `manage_editor` gains undo/redo actions. `manage_scene` gains multi-scene editing (additive load, close, set active, move GO between scenes), scene templates (3d_basic, 2d_basic, etc.), and scene validation with auto-repair. New `manage_build` tool: trigger player builds, switch platforms, configure player settings, manage build scenes and profiles (Unity 6+), run batch builds across multiple platforms, and async job tracking with polling. New `MaxPollSeconds` infrastructure for long-running tool operations.

## v9.5 series

- **v9.5.4** — New `unity_reflect` and `unity_docs` tools for API verification: inspect live C# APIs via reflection and fetch official Unity documentation (ScriptReference, Manual, package docs). New `manage_packages` tool: install, remove, search, and manage Unity packages and scoped registries. Includes input validation, dependency checks on removal, and git URL warnings.
- **v9.5.3** — New `manage_graphics` tool (33 actions): volume/post-processing, light baking, rendering stats, pipeline settings, URP renderer features. 3 new resources: `volumes`, `rendering_stats`, `renderer_features`.
- **v9.5.2** — New `manage_camera` tool with Cinemachine support (presets, priority, noise, blending, extensions), `cameras` resource, priority persistence fix via `SerializedProperty`.

## v9.4 series

- **v9.4.8** — New editor UI, real-time tool toggling via `manage_tools`, skill sync window, multi-view screenshot, one-click Roslyn installer, Qwen Code & Gemini CLI clients, ProBuilder mesh editing via `manage_probuilder`.
- **v9.4.7** — Per-call Unity instance routing, macOS pyenv PATH fix, domain reload resilience for script tools.
- **v9.4.6** — New `manage_animation` tool, Cline client support, stale connection detection, tool state persistence across reloads.
- **v9.4.4** — Configurable `batch_execute` limits, tool filtering by session state, IPv6/IPv4 loopback fixes.

## Migration guides

Breaking changes from prior major versions live under [Migrations](/migrations/v5):

- [v5 — UnityMcpBridge → MCPForUnity](/migrations/v5)
- [v6 — New Editor Window (UI Toolkit + service architecture)](/migrations/v6)
- [v8 — HTTP and Stdio support](/migrations/v8)
