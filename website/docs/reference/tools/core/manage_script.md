---
title: manage_script
sidebar_label: manage_script
description: "Compatibility router for legacy script operations"
---

# `manage_script`

> **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_script`

## Description

Compatibility router for legacy script operations. Prefer apply_text_edits (ranges) or script_apply_edits (structured) for edits. Read-only action: read. Modifying actions: create, delete.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['create', 'read', 'delete']` | yes | Perform CRUD operations on C# scripts. |
| `name` | `str` | yes | Script name (no .cs extension) |
| `path` | `str` | yes | Asset path (default: 'Assets/') |
| `contents` | `str \| None` | — | Contents of the script to create |
| `script_type` | `str \| None` | — | Script type (e.g., 'C#') |
| `namespace` | `str \| None` | — | Namespace for the script |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

