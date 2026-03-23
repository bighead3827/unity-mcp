"""Tests for manage_packages tool and CLI commands."""

import asyncio
import pytest
from unittest.mock import patch, MagicMock, AsyncMock
from click.testing import CliRunner

from cli.commands.packages import packages
from cli.utils.config import CLIConfig
from services.tools.manage_packages import ALL_ACTIONS


# =============================================================================
# Fixtures
# =============================================================================

@pytest.fixture
def runner():
    return CliRunner()


@pytest.fixture
def mock_config():
    return CLIConfig(
        host="127.0.0.1",
        port=8080,
        timeout=30,
        format="text",
        unity_instance=None,
    )


@pytest.fixture
def mock_success():
    return {"success": True, "message": "OK", "data": {}}


# =============================================================================
# Action Lists
# =============================================================================

class TestActionLists:
    """Verify action list completeness and consistency."""

    def test_all_actions_is_not_empty(self):
        assert len(ALL_ACTIONS) > 0

    def test_no_duplicate_actions(self):
        assert len(ALL_ACTIONS) == len(set(ALL_ACTIONS))

    def test_expected_query_actions_present(self):
        expected = {"list_packages", "search_packages", "get_package_info", "ping", "status"}
        assert expected.issubset(set(ALL_ACTIONS))

    def test_expected_install_remove_actions_present(self):
        expected = {"add_package", "remove_package", "embed_package", "resolve_packages"}
        assert expected.issubset(set(ALL_ACTIONS))

    def test_expected_registry_actions_present(self):
        expected = {"add_registry", "remove_registry", "list_registries"}
        assert expected.issubset(set(ALL_ACTIONS))


# =============================================================================
# Tool Validation (Python-side, no Unity)
# =============================================================================

class TestManagePackagesToolValidation:
    """Test action validation in the manage_packages tool function."""

    def test_unknown_action_returns_error(self):
        from services.tools.manage_packages import manage_packages

        ctx = MagicMock()
        ctx.get_state = AsyncMock(return_value=None)

        result = asyncio.run(manage_packages(ctx, action="invalid_action"))
        assert result["success"] is False
        assert "Unknown action" in result["message"]

    def test_unknown_action_lists_valid_actions(self):
        from services.tools.manage_packages import manage_packages

        ctx = MagicMock()
        ctx.get_state = AsyncMock(return_value=None)

        result = asyncio.run(manage_packages(ctx, action="bogus"))
        assert result["success"] is False
        # The error message should mention the valid actions
        assert "add_package" in result["message"] or "Valid actions" in result["message"]

    def test_action_matching_is_case_insensitive(self):
        from services.tools.manage_packages import manage_packages

        ctx = MagicMock()
        ctx.get_state = AsyncMock(return_value=None)

        # Should not return "Unknown action" — it should try to send to Unity
        # We patch _send_packages_command to avoid actually connecting
        with patch("services.tools.manage_packages._send_packages_command", new_callable=AsyncMock) as mock_send:
            mock_send.return_value = {"success": True, "message": "OK"}
            result = asyncio.run(manage_packages(ctx, action="LIST_PACKAGES"))
        assert result["success"] is True


# =============================================================================
# CLI Command Parameter Building
# =============================================================================

def _get_params(mock_run):
    """Helper to extract the params dict from a mock run_command call."""
    return mock_run.call_args[0][1]


class TestPackagesQueryCLICommands:
    """Verify query CLI commands build correct parameter dicts."""

    def test_list_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["list"])

                mock_run.assert_called_once()
                params = _get_params(mock_run)
                assert params["action"] == "list_packages"

    def test_search_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["search", "input"])

                params = _get_params(mock_run)
                assert params["action"] == "search_packages"
                assert params["query"] == "input"

    def test_info_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["info", "com.unity.inputsystem"])

                params = _get_params(mock_run)
                assert params["action"] == "get_package_info"
                assert params["package"] == "com.unity.inputsystem"

    def test_ping_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["ping"])

                params = _get_params(mock_run)
                assert params["action"] == "ping"

    def test_status_without_job_id(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["status"])

                params = _get_params(mock_run)
                assert params["action"] == "status"
                assert "job_id" not in params

    def test_status_with_job_id(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["status", "abc123"])

                params = _get_params(mock_run)
                assert params["action"] == "status"
                assert params["job_id"] == "abc123"


class TestPackagesInstallRemoveCLICommands:
    """Verify install/remove CLI commands build correct parameter dicts."""

    def test_add_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["add", "com.unity.inputsystem"])

                params = _get_params(mock_run)
                assert params["action"] == "add_package"
                assert params["package"] == "com.unity.inputsystem"

    def test_add_with_version_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["add", "com.unity.inputsystem@1.8.0"])

                params = _get_params(mock_run)
                assert params["action"] == "add_package"
                assert params["package"] == "com.unity.inputsystem@1.8.0"

    def test_remove_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["remove", "com.unity.inputsystem"])

                params = _get_params(mock_run)
                assert params["action"] == "remove_package"
                assert params["package"] == "com.unity.inputsystem"
                assert "force" not in params

    def test_remove_with_force_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["remove", "com.unity.inputsystem", "--force"])

                params = _get_params(mock_run)
                assert params["action"] == "remove_package"
                assert params["force"] is True

    def test_embed_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["embed", "com.unity.timeline"])

                params = _get_params(mock_run)
                assert params["action"] == "embed_package"
                assert params["package"] == "com.unity.timeline"

    def test_resolve_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["resolve"])

                params = _get_params(mock_run)
                assert params["action"] == "resolve_packages"


class TestRegistryCLICommands:
    """Verify registry CLI commands build correct parameter dicts."""

    def test_list_registries_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["list-registries"])

                params = _get_params(mock_run)
                assert params["action"] == "list_registries"

    def test_add_registry_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, [
                    "add-registry", "OpenUPM",
                    "--url", "https://package.openupm.com",
                    "--scope", "com.cysharp",
                    "--scope", "com.neuecc",
                ])

                params = _get_params(mock_run)
                assert params["action"] == "add_registry"
                assert params["name"] == "OpenUPM"
                assert params["url"] == "https://package.openupm.com"
                assert params["scopes"] == ["com.cysharp", "com.neuecc"]

    def test_add_registry_with_single_scope(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, [
                    "add-registry", "MyReg",
                    "--url", "https://registry.example.com",
                    "--scope", "com.example",
                ])

                params = _get_params(mock_run)
                assert params["scopes"] == ["com.example"]

    def test_remove_registry_builds_correct_params(self, runner, mock_config, mock_success):
        with patch("cli.commands.packages.get_config", return_value=mock_config):
            with patch("cli.commands.packages.run_command", return_value=mock_success) as mock_run:
                runner.invoke(packages, ["remove-registry", "OpenUPM"])

                params = _get_params(mock_run)
                assert params["action"] == "remove_registry"
                assert params["name"] == "OpenUPM"
