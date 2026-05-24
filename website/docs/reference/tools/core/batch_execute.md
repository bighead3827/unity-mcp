---
title: batch_execute
sidebar_label: batch_execute
description: "Executes multiple MCP commands in a single batch for dramatically better performance"
---

# `batch_execute`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.batch_execute`

## Description

Executes multiple MCP commands in a single batch for dramatically better performance. STRONGLY RECOMMENDED when creating/modifying multiple objects, adding components to multiple targets, or performing any repetitive operations. Reduces latency and token costs by 10-100x compared to sequential tool calls. The max commands per batch is configurable in the Unity MCP Tools window (default 25, hard max 100). Example: creating 5 cubes → use 1 batch_execute with 5 create commands instead of 5 separate calls.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `commands` | `list[dict[str, Any]]` | yes | List of commands with 'tool' and 'params' keys. |
| `parallel` | `bool \| None` | — | Attempt to run read-only commands in parallel |
| `fail_fast` | `bool \| None` | — | Stop processing after the first failure |
| `max_parallelism` | `int \| None` | — | Hint for the maximum number of parallel workers |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

