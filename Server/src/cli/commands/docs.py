"""Unity documentation lookup CLI commands."""

import asyncio
import click

from cli.utils.config import get_config
from cli.utils.output import format_output


@click.group()
def docs():
    """Fetch Unity API documentation."""
    pass


@docs.command("get")
@click.argument("class_name")
@click.argument("member_name", required=False)
@click.option("--version", "-v", default=None, help="Unity version (e.g., 6000.0).")
def get_doc(class_name: str, member_name: str | None, version: str | None):
    """Fetch documentation for a Unity class or member.

    \b
    Examples:
        unity-mcp docs get Physics
        unity-mcp docs get Physics Raycast
        unity-mcp docs get NavMeshAgent SetDestination --version 6000.0
    """
    from services.tools.unity_docs import _extract_version, _build_doc_url, _build_property_url, _fetch_url, _parse_unity_doc_html

    config = get_config()
    resolved_version = _extract_version(version)

    async def _run():
        url = _build_doc_url(class_name, member_name, resolved_version)
        try:
            status, html = await _fetch_url(url)
            url_used = url

            if status == 404 and member_name:
                prop_url = _build_property_url(class_name, member_name, resolved_version)
                status, html = await _fetch_url(prop_url)
                if status == 200:
                    url_used = prop_url

            if status == 404 and resolved_version:
                fallback = _build_doc_url(class_name, member_name, None)
                status, html = await _fetch_url(fallback)
                if status == 200:
                    url_used = fallback
                elif member_name:
                    fallback_prop = _build_property_url(class_name, member_name, None)
                    status, html = await _fetch_url(fallback_prop)
                    if status == 200:
                        url_used = fallback_prop

            if status == 404:
                return {"success": True, "data": {"found": False, "class": class_name, "member": member_name}}

            parsed = _parse_unity_doc_html(html)
            return {"success": True, "data": {"found": True, "url": url_used, "class": class_name, "member": member_name, **parsed}}
        except ConnectionError as e:
            return {"success": False, "message": str(e)}

    result = asyncio.run(_run())
    click.echo(format_output(result, config.format))
