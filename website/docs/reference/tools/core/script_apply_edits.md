---
title: script_apply_edits
sidebar_label: script_apply_edits
description: "Structured C# edits (methods/classes) with safer boundaries - prefer this over raw text"
---

# `script_apply_edits`

> ⚙️ **Auto-generated** from the Python tool registry. Do not hand-edit outside `<!-- examples:start --><!-- examples:end -->` blocks — the generator (`tools/generate_docs_reference.py`) will overwrite them.

**Group:** `core` &nbsp;·&nbsp; **Module:** `services.tools.script_apply_edits`

## Description

Structured C# edits (methods/classes) with safer boundaries - prefer this over raw text.
    Best practices:
    - Prefer anchor_* ops for pattern-based insert/replace near stable markers
    - Use replace_method/delete_method for whole-method changes (keeps signatures balanced)
    - Avoid whole-file regex deletes; validators will guard unbalanced braces
    - For tail insertions, prefer anchor/regex_replace on final brace (class closing)
    - Pass options.validate='standard' for structural checks; 'basic' for interior-only edits
    Canonical fields (use these exact keys):
    - op: replace_method | insert_method | delete_method | anchor_insert | anchor_delete | anchor_replace
    - className: string (defaults to 'name' if omitted on method/class ops)
    - methodName: string (required for replace_method, delete_method)
    - replacement: string (required for replace_method, insert_method)
    - position: start | end | after | before (insert_method only)
    - afterMethodName / beforeMethodName: string (required when position='after'/'before')
    - anchor: regex string (for anchor_* ops)
    - text: string (for anchor_insert/anchor_replace)
    Examples:
    1) Replace a method:
    {
        "name": "SmartReach",
        "path": "Assets/Scripts/Interaction",
        "edits": [
        {
        "op": "replace_method",
        "className": "SmartReach",
        "methodName": "HasTarget",
        "replacement": "public bool HasTarget(){ return currentTarget!=null; }"
        }
    ],
    "options": {"validate": "standard", "refresh": "immediate"}
    }
    "2) Insert a method after another:
    {
        "name": "SmartReach",
        "path": "Assets/Scripts/Interaction",
        "edits": [
        {
        "op": "insert_method",
        "className": "SmartReach",
        "replacement": "public void PrintSeries(){ Debug.Log(seriesName); }",
        "position": "after",
        "afterMethodName": "GetCurrentTarget"
        }
    ],
    }
    ]

## Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `name` | `str` | ✅ | Name of the script to edit |
| `path` | `str` | ✅ | Path to the script to edit under Assets/ directory |
| `edits` | `list[dict[str, Any]] \| str` | ✅ | List of edits to apply to the script (JSON list or stringified JSON) |
| `options` | `dict[str, Any] \| None` | — | Options for the script edit |
| `script_type` | `str` | — | Type of the script to edit |
| `namespace` | `str \| None` | — | Namespace of the script to edit |

## Returns

A `dict` containing the Unity response. The exact shape depends on the action.

## Examples

<!-- examples:start -->
*No examples yet. Add usage examples here — they will be preserved across regenerations.*
<!-- examples:end -->

