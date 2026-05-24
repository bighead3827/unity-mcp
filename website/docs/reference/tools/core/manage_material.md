---
title: manage_material
sidebar_label: manage_material
description: "Manages Unity materials (set properties, colors, shaders, etc)"
---

# `manage_material`

> ⚙️ **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.manage_material`

## Description

Manages Unity materials (set properties, colors, shaders, etc). Read-only actions: ping, get_material_info. Modifying actions: create, set_material_shader_property, set_material_color, assign_material_to_renderer, set_renderer_color.

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `action` | `Literal['ping', 'create', 'set_material_shader_property', 'set_material_color', 'assign_material_to_renderer', 'set_renderer_color', 'get_material_info']` | ✅ | Action to perform. |
| `material_path` | `str \| None` | — | Path to material asset (Assets/...) |
| `property` | `str \| None` | — | Shader property name (e.g., _BaseColor, _MainTex) |
| `shader` | `str \| None` | — | Shader name (default: Standard) |
| `properties` | `dict[str, Any] \| str \| None` | — | Initial properties to set as {name: value} dict. |
| `value` | `list \| float \| int \| str \| bool \| None` | — | Value to set (color array, float, texture path/instruction) |
| `color` | `list[float] \| dict[str, float] \| str \| None` | — | Color as [r, g, b] or [r, g, b, a] array, {r, g, b, a} object, or JSON string. |
| `target` | `str \| None` | — | Target GameObject (name, path, or find instruction) |
| `search_method` | `Literal['by_id', 'by_name', 'by_path', 'by_tag', 'by_layer', 'by_component'] \| None` | — | Search method for target |
| `slot` | `int \| None` | — | Material slot index (0-based) |
| `mode` | `Literal['shared', 'instance', 'property_block', 'create_unique'] \| None` | — | Assignment/modification mode; behavior when omitted is action-specific on the Unity side. |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

