from typing import Annotated, Any, Optional

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool
from services.tools import get_unity_instance_from_context
from transport.unity_transport import send_with_unity_instance
from transport.legacy.unity_connection import async_send_command_with_retry

SETTINGS_ACTIONS = ["ping", "get_settings", "set_settings"]

COLLISION_MATRIX_ACTIONS = ["get_collision_matrix", "set_collision_matrix"]

MATERIAL_ACTIONS = [
    "create_physics_material",
    "configure_physics_material",
    "assign_physics_material",
]

JOINT_ACTIONS = ["add_joint", "configure_joint", "remove_joint"]

QUERY_ACTIONS = ["raycast", "overlap"]

VALIDATION_ACTIONS = ["validate"]

SIMULATION_ACTIONS = ["simulate_step"]

ALL_ACTIONS = (
    SETTINGS_ACTIONS
    + COLLISION_MATRIX_ACTIONS
    + MATERIAL_ACTIONS
    + JOINT_ACTIONS
    + QUERY_ACTIONS
    + VALIDATION_ACTIONS
    + SIMULATION_ACTIONS
)


@mcp_for_unity_tool(
    group="core",
    description=(
        "Manage physics settings, collision matrix, materials, joints, queries, and validation.\n\n"
        "SETTINGS: ping, get_settings, set_settings\n"
        "COLLISION MATRIX: get_collision_matrix, set_collision_matrix\n"
        "MATERIALS: create_physics_material, configure_physics_material, assign_physics_material\n"
        "JOINTS: add_joint, configure_joint, remove_joint\n"
        "QUERIES: raycast, overlap\n"
        "VALIDATION: validate\n"
        "SIMULATION: simulate_step\n"
    ),
    annotations=ToolAnnotations(title="Manage Physics", destructiveHint=True),
)
async def manage_physics(
    ctx: Context,
    action: Annotated[str, "The physics action to perform."],
    dimension: Annotated[Optional[str], "Physics dimension: '3d' (default) or '2d'."] = None,
    settings: Annotated[
        Optional[dict[str, Any]], "Key-value settings for set_settings."
    ] = None,
    layer_a: Annotated[
        Optional[str], "Layer name or index for collision matrix."
    ] = None,
    layer_b: Annotated[
        Optional[str], "Layer name or index for collision matrix."
    ] = None,
    collide: Annotated[
        Optional[bool], "Whether layers should collide (set_collision_matrix)."
    ] = None,
    name: Annotated[Optional[str], "Name for new physics material."] = None,
    path: Annotated[Optional[str], "Asset path for materials."] = None,
    dynamic_friction: Annotated[Optional[float], "Dynamic friction (0-1)."] = None,
    static_friction: Annotated[Optional[float], "Static friction (0-1)."] = None,
    bounciness: Annotated[Optional[float], "Bounciness (0-1)."] = None,
    friction: Annotated[Optional[float], "Friction for 2D materials."] = None,
    friction_combine: Annotated[
        Optional[str], "Friction combine mode: Average, Minimum, Multiply, Maximum."
    ] = None,
    bounce_combine: Annotated[
        Optional[str], "Bounce combine mode: Average, Minimum, Multiply, Maximum."
    ] = None,
    material_path: Annotated[
        Optional[str], "Path to physics material asset for assign."
    ] = None,
    target: Annotated[
        Optional[str], "Target GameObject name or instance ID."
    ] = None,
    collider_type: Annotated[
        Optional[str], "Specific collider type to target."
    ] = None,
    search_method: Annotated[
        Optional[str], "Search method for target resolution."
    ] = None,
    joint_type: Annotated[
        Optional[str],
        "Joint type: fixed, hinge, spring, character, configurable (3D); "
        "distance, fixed, friction, hinge, relative, slider, spring, target, wheel (2D).",
    ] = None,
    connected_body: Annotated[
        Optional[str], "Connected body target for joints."
    ] = None,
    motor: Annotated[
        Optional[dict[str, Any]],
        "Motor config: {targetVelocity, force, freeSpin}.",
    ] = None,
    limits: Annotated[
        Optional[dict[str, Any]], "Limits config: {min, max, bounciness}."
    ] = None,
    spring: Annotated[
        Optional[dict[str, Any]],
        "Spring config: {spring, damper, targetPosition}.",
    ] = None,
    drive: Annotated[
        Optional[dict[str, Any]],
        "Drive config for ConfigurableJoint.",
    ] = None,
    properties: Annotated[
        Optional[dict[str, Any]], "Direct property dict for joints or materials."
    ] = None,
    origin: Annotated[
        Optional[list[float]], "Ray origin [x,y,z] or [x,y]."
    ] = None,
    direction: Annotated[
        Optional[list[float]], "Ray direction [x,y,z] or [x,y]."
    ] = None,
    max_distance: Annotated[Optional[float], "Max raycast distance."] = None,
    layer_mask: Annotated[
        Optional[str], "Layer mask for queries (name or int)."
    ] = None,
    query_trigger_interaction: Annotated[
        Optional[str], "Trigger interaction: UseGlobal, Ignore, Collide."
    ] = None,
    shape: Annotated[
        Optional[str], "Overlap shape: sphere, box, capsule (3D); circle, box, capsule (2D)."
    ] = None,
    position: Annotated[
        Optional[list[float]], "Overlap position [x,y,z] or [x,y]."
    ] = None,
    size: Annotated[
        Optional[Any], "Overlap size: float (radius) or [x,y,z] (half-extents)."
    ] = None,
    steps: Annotated[Optional[int], "Number of simulation steps (max 100)."] = None,
    step_size: Annotated[Optional[float], "Step size in seconds."] = None,
) -> dict[str, Any]:
    """Manage 3D and 2D physics: settings, collision matrix, materials, joints, queries, validation, simulation."""

    action_lower = action.lower()
    if action_lower not in ALL_ACTIONS:
        return {
            "success": False,
            "message": f"Unknown action '{action}'. Valid: {', '.join(ALL_ACTIONS)}",
        }

    unity_instance = await get_unity_instance_from_context(ctx)

    params_dict: dict[str, Any] = {"action": action_lower}

    param_map = {
        "dimension": dimension,
        "settings": settings,
        "layer_a": layer_a,
        "layer_b": layer_b,
        "collide": collide,
        "name": name,
        "path": path,
        "dynamic_friction": dynamic_friction,
        "static_friction": static_friction,
        "bounciness": bounciness,
        "friction": friction,
        "friction_combine": friction_combine,
        "bounce_combine": bounce_combine,
        "material_path": material_path,
        "target": target,
        "collider_type": collider_type,
        "search_method": search_method,
        "joint_type": joint_type,
        "connected_body": connected_body,
        "motor": motor,
        "limits": limits,
        "spring": spring,
        "drive": drive,
        "properties": properties,
        "origin": origin,
        "direction": direction,
        "max_distance": max_distance,
        "layer_mask": layer_mask,
        "query_trigger_interaction": query_trigger_interaction,
        "shape": shape,
        "position": position,
        "size": size,
        "steps": steps,
        "step_size": step_size,
    }
    for key, val in param_map.items():
        if val is not None:
            params_dict[key] = val

    result = await send_with_unity_instance(
        async_send_command_with_retry, unity_instance, "manage_physics", params_dict
    )
    return result if isinstance(result, dict) else {"success": False, "message": str(result)}
