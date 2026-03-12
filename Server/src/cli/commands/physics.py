import click

from cli.utils.connection import handle_unity_errors, run_command, get_config
from cli.utils.output import format_output


@click.group("physics")
def physics():
    """Manage 3D and 2D physics: settings, collision matrix, materials, joints, queries, validation."""
    pass


@physics.command("ping")
@handle_unity_errors
def ping():
    """Check physics system status."""
    config = get_config()
    result = run_command("manage_physics", {"action": "ping"}, config)
    click.echo(format_output(result, config.format))


@physics.command("get-settings")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@handle_unity_errors
def get_settings(dimension):
    """Get physics project settings."""
    config = get_config()
    result = run_command(
        "manage_physics", {"action": "get_settings", "dimension": dimension}, config
    )
    click.echo(format_output(result, config.format))


@physics.command("set-settings")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@click.argument("key")
@click.argument("value")
@handle_unity_errors
def set_settings(dimension, key, value):
    """Set a physics setting (key value)."""
    config = get_config()
    coerced = value
    if value.lower() in ("true", "false"):
        coerced = value.lower() == "true"
    else:
        try:
            coerced = float(value) if "." in value else int(value)
        except ValueError:
            pass
    result = run_command(
        "manage_physics",
        {"action": "set_settings", "dimension": dimension, "settings": {key: coerced}},
        config,
    )
    click.echo(format_output(result, config.format))


@physics.command("get-collision-matrix")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@handle_unity_errors
def get_collision_matrix(dimension):
    """Get layer collision matrix."""
    config = get_config()
    result = run_command(
        "manage_physics",
        {"action": "get_collision_matrix", "dimension": dimension},
        config,
    )
    click.echo(format_output(result, config.format))


@physics.command("set-collision-matrix")
@click.argument("layer_a")
@click.argument("layer_b")
@click.option("--collide/--ignore", default=True, help="Enable or disable collision.")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@handle_unity_errors
def set_collision_matrix(layer_a, layer_b, collide, dimension):
    """Set collision between two layers."""
    config = get_config()
    result = run_command(
        "manage_physics",
        {
            "action": "set_collision_matrix",
            "layer_a": layer_a,
            "layer_b": layer_b,
            "collide": collide,
            "dimension": dimension,
        },
        config,
    )
    click.echo(format_output(result, config.format))


@physics.command("create-material")
@click.option("--name", "-n", required=True, help="Material name.")
@click.option("--path", "-p", default=None, help="Folder path.")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@click.option("--bounciness", "-b", type=float, default=None, help="Bounciness (0-1).")
@click.option("--dynamic-friction", type=float, default=None, help="Dynamic friction.")
@click.option("--static-friction", type=float, default=None, help="Static friction.")
@click.option("--friction", type=float, default=None, help="Friction (2D only).")
@handle_unity_errors
def create_material(name, path, dimension, bounciness, dynamic_friction, static_friction, friction):
    """Create a physics material asset."""
    config = get_config()
    params = {
        "action": "create_physics_material",
        "name": name,
        "dimension": dimension,
    }
    if path:
        params["path"] = path
    if bounciness is not None:
        params["bounciness"] = bounciness
    if dynamic_friction is not None:
        params["dynamic_friction"] = dynamic_friction
    if static_friction is not None:
        params["static_friction"] = static_friction
    if friction is not None:
        params["friction"] = friction
    result = run_command("manage_physics", params, config)
    click.echo(format_output(result, config.format))


@physics.command("validate")
@click.option("--target", "-t", default=None, help="Target GameObject (or whole scene).")
@click.option("--dimension", "-d", default="both", help="3d, 2d, or both.")
@handle_unity_errors
def validate(target, dimension):
    """Validate physics setup for common mistakes."""
    config = get_config()
    params = {"action": "validate", "dimension": dimension}
    if target:
        params["target"] = target
    result = run_command("manage_physics", params, config)
    click.echo(format_output(result, config.format))


@physics.command("raycast")
@click.option("--origin", "-o", required=True, help="Origin as 'x,y,z'.")
@click.option("--direction", "-d", required=True, help="Direction as 'x,y,z'.")
@click.option("--max-distance", type=float, default=None, help="Max distance.")
@click.option("--dimension", default="3d", help="3d or 2d.")
@handle_unity_errors
def raycast(origin, direction, max_distance, dimension):
    """Perform a physics raycast."""
    config = get_config()
    params = {
        "action": "raycast",
        "origin": [float(x) for x in origin.split(",")],
        "direction": [float(x) for x in direction.split(",")],
        "dimension": dimension,
    }
    if max_distance is not None:
        params["max_distance"] = max_distance
    result = run_command("manage_physics", params, config)
    click.echo(format_output(result, config.format))


@physics.command("simulate")
@click.option("--steps", "-s", type=int, default=1, help="Number of steps (max 100).")
@click.option("--step-size", type=float, default=None, help="Step size in seconds.")
@click.option("--dimension", "-d", default="3d", help="3d or 2d.")
@handle_unity_errors
def simulate(steps, step_size, dimension):
    """Run physics simulation steps in edit mode."""
    config = get_config()
    params = {"action": "simulate_step", "steps": steps, "dimension": dimension}
    if step_size is not None:
        params["step_size"] = step_size
    result = run_command("manage_physics", params, config)
    click.echo(format_output(result, config.format))
