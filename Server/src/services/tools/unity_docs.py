import asyncio
import re
from html.parser import HTMLParser
from typing import Annotated, Any, Optional
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from fastmcp import Context
from mcp.types import ToolAnnotations

from services.registry import mcp_for_unity_tool

ALL_ACTIONS = ["get_doc"]


# ---------------------------------------------------------------------------
# Version extraction
# ---------------------------------------------------------------------------

def _extract_version(version_str: str | None) -> str | None:
    """Extract major.minor from a full Unity version string.

    Examples:
        "6000.0.38f1" -> "6000.0"
        "2022.3.45f1" -> "2022.3"
        "6000.1.0b2"  -> "6000.1"
        None           -> None
        ""             -> None
    """
    if not version_str:
        return None
    parts = version_str.split(".")
    if len(parts) < 2:
        return version_str
    second = re.sub(r"[a-zA-Z].*$", "", parts[1])
    return f"{parts[0]}.{second}"


# ---------------------------------------------------------------------------
# URL construction
# ---------------------------------------------------------------------------

def _build_doc_url(
    class_name: str,
    member_name: str | None,
    version: str | None,
) -> str:
    """Build the ScriptReference URL using dot separator for members."""
    if member_name:
        page = f"{class_name}.{member_name}.html"
    else:
        page = f"{class_name}.html"

    if version:
        return f"https://docs.unity3d.com/{version}/Documentation/ScriptReference/{page}"
    return f"https://docs.unity3d.com/ScriptReference/{page}"


def _build_property_url(
    class_name: str,
    member_name: str,
    version: str | None,
) -> str:
    """Build the ScriptReference URL using dash separator (property style)."""
    page = f"{class_name}-{member_name}.html"
    if version:
        return f"https://docs.unity3d.com/{version}/Documentation/ScriptReference/{page}"
    return f"https://docs.unity3d.com/ScriptReference/{page}"


# ---------------------------------------------------------------------------
# HTTP fetch
# ---------------------------------------------------------------------------

async def _fetch_url(url: str) -> tuple[int, str]:
    """Fetch a URL and return (status_code, body_text).

    Runs urllib in an executor to avoid blocking the event loop.
    """
    loop = asyncio.get_running_loop()

    def _do_fetch() -> tuple[int, str]:
        req = Request(url, headers={"User-Agent": "MCPForUnity/1.0"})
        try:
            with urlopen(req, timeout=10) as resp:
                return (resp.status, resp.read().decode("utf-8", errors="replace"))
        except HTTPError as e:
            return (e.code, "")
        except URLError as e:
            raise ConnectionError(f"Cannot reach {url}: {e}") from e

    return await loop.run_in_executor(None, _do_fetch)


# ---------------------------------------------------------------------------
# HTML parser
# ---------------------------------------------------------------------------

class _UnityDocParser(HTMLParser):
    """Extracts structured data from Unity ScriptReference HTML pages."""

    def __init__(self) -> None:
        super().__init__()
        # Tracking state
        self._in_subsection = False
        self._subsection_title: str | None = None
        self._in_signature = False
        self._in_pre = False
        self._in_code_example = False
        self._in_param_table = False
        self._in_td = False
        self._td_class: str | None = None
        self._in_h2 = False
        self._in_p = False
        self._current_param: dict[str, str] = {}
        self._current_text: list[str] = []

        # Collected results
        self.description = ""
        self.signatures: list[str] = []
        self.parameters: list[dict[str, str]] = []
        self.returns = ""
        self.examples: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attr_dict = dict(attrs)
        classes = (attr_dict.get("class") or "").split()

        if tag == "div" and "subsection" in classes:
            self._in_subsection = True
            self._subsection_title = None

        if tag == "div" and ("signature" in classes or "signature-CS" in classes):
            self._in_signature = True

        # Unity docs use h3 (not h2) for subsection titles
        if tag in ("h2", "h3") and self._in_subsection:
            self._in_h2 = True
            self._current_text = []

        # Signatures: capture text inside signature-CS div (no <pre> in modern docs)
        if tag == "div" and "signature-CS" in classes:
            self._in_signature = True
            self._current_text = []

        if tag == "pre":
            if "codeExampleCS" in classes:
                self._in_code_example = True
                self._current_text = []
            elif self._in_signature:
                self._in_pre = True
                self._current_text = []

        if tag == "p" and self._in_subsection:
            self._in_p = True
            self._current_text = []

        if tag == "table" and self._in_subsection:
            self._in_param_table = True

        if tag == "td" and self._in_param_table:
            self._in_td = True
            self._td_class = attr_dict.get("class", "")
            self._current_text = []

        if tag == "tr" and self._in_param_table:
            self._current_param = {}

    def handle_endtag(self, tag: str) -> None:
        if tag in ("h2", "h3") and self._in_h2:
            self._in_h2 = False
            self._subsection_title = "".join(self._current_text).strip()

        if tag == "pre":
            if self._in_code_example:
                self._in_code_example = False
                self.examples.append("".join(self._current_text).strip())
            elif self._in_pre:
                self._in_pre = False
                self.signatures.append("".join(self._current_text).strip())

        if tag == "div" and self._in_signature:
            # Capture inline signature text (modern Unity docs don't use <pre>)
            text = " ".join("".join(self._current_text).split()).strip()
            # Remove "Declaration" prefix that appears inside the sig block
            if text.startswith("Declaration"):
                text = text[len("Declaration"):].strip()
            if text:
                self.signatures.append(text)
            self._in_signature = False

        if tag == "p" and self._in_p:
            self._in_p = False
            text = "".join(self._current_text).strip()
            if text and self._subsection_title == "Description" and not self.description:
                self.description = text
            elif text and self._subsection_title == "Returns" and not self.returns:
                self.returns = text

        if tag == "td" and self._in_td:
            self._in_td = False
            text = "".join(self._current_text).strip()
            # Support both old ("name-collumn"/"desc-collumn") and new ("name lbl"/"desc") class names
            if self._td_class and ("name-collumn" in self._td_class or "name" in self._td_class.split()):
                self._current_param["name"] = text
            elif self._td_class and ("desc-collumn" in self._td_class or "desc" in self._td_class.split()):
                self._current_param["description"] = text

        if tag == "tr" and self._in_param_table:
            if self._current_param.get("name"):
                self.parameters.append(dict(self._current_param))
            self._current_param = {}

        if tag == "table" and self._in_param_table:
            self._in_param_table = False

        if tag == "div" and self._in_subsection:
            self._in_subsection = False

    def handle_data(self, data: str) -> None:
        if self._in_h2 or self._in_pre or self._in_code_example or self._in_p or self._in_td or self._in_signature:
            self._current_text.append(data)


def _parse_unity_doc_html(html: str) -> dict[str, Any]:
    """Parse Unity ScriptReference HTML into structured data."""
    parser = _UnityDocParser()
    parser.feed(html)
    return {
        "description": parser.description,
        "signatures": parser.signatures,
        "parameters": parser.parameters,
        "returns": parser.returns,
        "examples": parser.examples,
        "see_also": [],
    }


# ---------------------------------------------------------------------------
# MCP tool
# ---------------------------------------------------------------------------

@mcp_for_unity_tool(
    unity_target="unity_reflect",
    group="docs",
    description=(
        "Fetch official Unity documentation for a class or member. "
        "Returns descriptions, parameter details, code examples, and caveats. "
        "Use after unity_reflect confirms a type exists, when you need richer "
        "context about behavior or usage patterns.\n\n"
        "Actions:\n"
        "- get_doc: Fetch docs for a class or member. Requires class_name. "
        "Optional member_name, version."
    ),
    annotations=ToolAnnotations(
        title="Unity Docs",
        readOnlyHint=True,
        destructiveHint=False,
    ),
)
async def unity_docs(
    ctx: Context,
    action: Annotated[str, "The documentation action to perform."],
    class_name: Annotated[Optional[str], "Unity class name (e.g. 'Physics', 'Transform')."] = None,
    member_name: Annotated[Optional[str], "Method or property name to look up."] = None,
    version: Annotated[Optional[str], "Unity version (e.g. '6000.0.38f1'). Auto-extracted."] = None,
) -> dict[str, Any]:
    action_lower = action.lower()
    if action_lower not in ALL_ACTIONS:
        return {
            "success": False,
            "message": f"Unknown action '{action}'. Valid actions: {', '.join(ALL_ACTIONS)}",
        }

    if action_lower == "get_doc":
        if not class_name:
            return {
                "success": False,
                "message": "get_doc requires class_name.",
            }
        return await _get_doc(class_name, member_name, version)

    return {"success": False, "message": "Unreachable"}


async def _get_doc(
    class_name: str,
    member_name: str | None,
    version: str | None,
) -> dict[str, Any]:
    extracted_version = _extract_version(version)

    url = _build_doc_url(class_name, member_name, extracted_version)

    try:
        status, body = await _fetch_url(url)

        # Member fallback: try property (dash) URL if method (dot) URL 404s
        if status == 404 and member_name:
            prop_url = _build_property_url(class_name, member_name, extracted_version)
            status, body = await _fetch_url(prop_url)
            if status == 200:
                url = prop_url

        # Version fallback: try versionless URL if versioned 404s
        if status == 404 and extracted_version:
            fallback_url = _build_doc_url(class_name, member_name, None)
            status, body = await _fetch_url(fallback_url)
            if status == 200:
                url = fallback_url
            elif member_name:
                # Also try property fallback without version
                prop_fallback = _build_property_url(class_name, member_name, None)
                status, body = await _fetch_url(prop_fallback)
                if status == 200:
                    url = prop_fallback

        if status == 404:
            return {
                "success": True,
                "data": {
                    "found": False,
                    "suggestion": (
                        "Try unity_reflect search action to verify the type name, "
                        "then retry with the correct class_name."
                    ),
                },
            }

        parsed = _parse_unity_doc_html(body)
        return {
            "success": True,
            "data": {
                "found": True,
                "url": url,
                "class": class_name,
                "member": member_name,
                "description": parsed["description"],
                "signatures": parsed["signatures"],
                "parameters": parsed["parameters"],
                "returns": parsed["returns"],
                "examples": parsed["examples"],
                "see_also": parsed["see_also"],
            },
        }

    except ConnectionError as e:
        return {
            "success": False,
            "message": f"Could not reach docs.unity3d.com: {e}",
        }
