from __future__ import annotations

import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

from services.tools.unity_docs import (
    unity_docs,
    ALL_ACTIONS,
    _extract_version,
    _build_doc_url,
    _build_property_url,
    _parse_unity_doc_html,
)


# ---------------------------------------------------------------------------
# Sample HTML for parser tests
# ---------------------------------------------------------------------------

SAMPLE_DOC_HTML = """\
<div class="subsection">
  <div class="signature">
    <pre>public static bool <strong>Raycast</strong>(Vector3 origin, Vector3 direction)</pre>
  </div>
</div>
<div class="subsection">
  <h2>Description</h2>
  <p>Casts a ray against all colliders in the Scene.</p>
</div>
<div class="subsection">
  <h2>Parameters</h2>
  <table>
    <tr>
      <td class="name-collumn"><strong>origin</strong></td>
      <td class="desc-collumn">The starting point of the ray in world coordinates.</td>
    </tr>
    <tr>
      <td class="name-collumn"><strong>direction</strong></td>
      <td class="desc-collumn">The direction of the ray.</td>
    </tr>
  </table>
</div>
<div class="subsection">
  <h2>Returns</h2>
  <p><strong>bool</strong> True when the ray intersects any collider.</p>
</div>
<div class="subsection">
  <h2>Examples</h2>
  <pre class="codeExampleCS">void Update() {
    if (Physics.Raycast(transform.position, transform.forward, 100))
        Debug.Log("Hit something");
}</pre>
</div>
"""


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------

@pytest.fixture
def ctx():
    return SimpleNamespace(info=AsyncMock(), warning=AsyncMock())


# ---------------------------------------------------------------------------
# Version extraction (pure)
# ---------------------------------------------------------------------------

def test_extract_version_full():
    assert _extract_version("6000.0.38f1") == "6000.0"


def test_extract_version_lts():
    assert _extract_version("2022.3.45f1") == "2022.3"


def test_extract_version_beta():
    assert _extract_version("6000.1.0b2") == "6000.1"


def test_extract_version_none():
    assert _extract_version(None) is None


def test_extract_version_empty():
    assert _extract_version("") is None


def test_extract_version_already_short():
    assert _extract_version("6000.0") == "6000.0"


# ---------------------------------------------------------------------------
# URL construction (pure)
# ---------------------------------------------------------------------------

def test_build_url_class_only():
    url = _build_doc_url("Physics", None, "6000.0")
    assert url == "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.html"


def test_build_url_with_member():
    url = _build_doc_url("Physics", "Raycast", "6000.0")
    assert url == "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Physics.Raycast.html"


def test_build_url_versionless():
    url = _build_doc_url("Physics", None, None)
    assert url == "https://docs.unity3d.com/ScriptReference/Physics.html"


def test_build_property_url():
    url = _build_property_url("Transform", "position", "6000.0")
    assert url == "https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Transform-position.html"


def test_build_property_url_versionless():
    url = _build_property_url("Transform", "position", None)
    assert url == "https://docs.unity3d.com/ScriptReference/Transform-position.html"


# ---------------------------------------------------------------------------
# HTML parsing (pure)
# ---------------------------------------------------------------------------

def test_parse_html_description():
    result = _parse_unity_doc_html(SAMPLE_DOC_HTML)
    assert "Casts a ray" in result["description"]


def test_parse_html_signatures():
    result = _parse_unity_doc_html(SAMPLE_DOC_HTML)
    assert len(result["signatures"]) >= 1
    assert "Raycast" in result["signatures"][0]


def test_parse_html_parameters():
    result = _parse_unity_doc_html(SAMPLE_DOC_HTML)
    assert len(result["parameters"]) == 2
    assert result["parameters"][0]["name"] == "origin"
    assert "starting point" in result["parameters"][0]["description"]
    assert result["parameters"][1]["name"] == "direction"


def test_parse_html_returns():
    result = _parse_unity_doc_html(SAMPLE_DOC_HTML)
    assert "True when" in result["returns"]


def test_parse_html_examples():
    result = _parse_unity_doc_html(SAMPLE_DOC_HTML)
    assert len(result["examples"]) >= 1
    assert "Physics.Raycast" in result["examples"][0]


def test_parse_empty_html():
    result = _parse_unity_doc_html("")
    assert result["description"] == ""
    assert result["signatures"] == []
    assert result["parameters"] == []
    assert result["returns"] == ""
    assert result["examples"] == []
    assert result["see_also"] == []


# ---------------------------------------------------------------------------
# Tool action tests (mock _fetch_url)
# ---------------------------------------------------------------------------

def test_unknown_action_returns_error():
    result = asyncio.run(unity_docs(SimpleNamespace(), action="bad_action"))
    assert result["success"] is False
    assert "Unknown action" in result["message"]


def test_get_doc_requires_class_name():
    result = asyncio.run(unity_docs(SimpleNamespace(), action="get_doc"))
    assert result["success"] is False
    assert "class_name" in result["message"]


def test_get_doc_success():
    async def mock_fetch(url):
        return (200, SAMPLE_DOC_HTML)

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(SimpleNamespace(), action="get_doc", class_name="Physics")
        )
    assert result["success"] is True
    assert result["data"]["found"] is True
    assert result["data"]["class"] == "Physics"
    assert "Casts a ray" in result["data"]["description"]
    assert len(result["data"]["signatures"]) >= 1
    assert len(result["data"]["parameters"]) == 2
    assert len(result["data"]["examples"]) >= 1


def test_get_doc_404():
    async def mock_fetch(url):
        return (404, "")

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(SimpleNamespace(), action="get_doc", class_name="FakeClass")
        )
    assert result["success"] is True
    assert result["data"]["found"] is False
    assert "suggestion" in result["data"]


def test_get_doc_property_fallback():
    """First fetch (dot URL) 404s, second fetch (dash URL) succeeds."""
    call_count = 0

    async def mock_fetch(url):
        nonlocal call_count
        call_count += 1
        if "-position" in url:
            return (200, SAMPLE_DOC_HTML)
        return (404, "")

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(
                SimpleNamespace(),
                action="get_doc",
                class_name="Transform",
                member_name="position",
            )
        )
    assert result["success"] is True
    assert result["data"]["found"] is True
    assert call_count == 2


def test_get_doc_network_error():
    async def mock_fetch(url):
        raise ConnectionError("Network unreachable")

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(SimpleNamespace(), action="get_doc", class_name="Physics")
        )
    assert result["success"] is False
    assert "Could not reach" in result["message"]


def test_get_doc_version_fallback():
    """Versioned URL 404s, versionless succeeds."""
    async def mock_fetch(url):
        if "/6000.0/" in url:
            return (404, "")
        return (200, SAMPLE_DOC_HTML)

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(
                SimpleNamespace(),
                action="get_doc",
                class_name="Physics",
                version="6000.0.38f1",
            )
        )
    assert result["success"] is True
    assert result["data"]["found"] is True
    assert "/6000.0/" not in result["data"]["url"]


def test_get_doc_with_member_and_version():
    async def mock_fetch(url):
        return (200, SAMPLE_DOC_HTML)

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(
                SimpleNamespace(),
                action="get_doc",
                class_name="Physics",
                member_name="Raycast",
                version="6000.0.38f1",
            )
        )
    assert result["success"] is True
    assert result["data"]["found"] is True
    assert result["data"]["member"] == "Raycast"
    assert "Physics.Raycast" in result["data"]["url"]


def test_get_doc_class_only_no_member_in_response():
    async def mock_fetch(url):
        return (200, SAMPLE_DOC_HTML)

    with patch("services.tools.unity_docs._fetch_url", side_effect=mock_fetch):
        result = asyncio.run(
            unity_docs(SimpleNamespace(), action="get_doc", class_name="Physics")
        )
    assert result["data"]["member"] is None


def test_all_actions_list():
    assert ALL_ACTIONS == ["get_doc"]


def test_no_duplicate_actions():
    assert len(ALL_ACTIONS) == len(set(ALL_ACTIONS))
