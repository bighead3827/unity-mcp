using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using UnityEditor;

namespace MCPForUnity.Editor.Services
{
    /// <summary>
    /// Once per Editor session, sweeps registered configurators and re-runs CheckStatus(attemptAutoRewrite: true)
    /// for any installed client that already has a config on disk. Catches the case where the user updated the
    /// MCP for Unity package while the Editor was closed — without this sweep, stale package versions in client
    /// configs would persist until the user opens the MCP window.
    /// </summary>
    [InitializeOnLoad]
    public static class StartupConfigRewrite
    {
        public const string SESSION_GUARD_KEY = "MCPForUnity.StartupConfigRewrite.Ran";

        static StartupConfigRewrite()
        {
            if (UnityEditorInternal.InternalEditorUtility.inBatchMode) return;
            if (SessionState.GetBool(SESSION_GUARD_KEY, false)) return;
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            if (SessionState.GetBool(SESSION_GUARD_KEY, false)) return;
            SessionState.SetBool(SESSION_GUARD_KEY, true);

            if (!EditorPrefs.GetBool(EditorPrefKeys.AutoRegisterEnabled, true)) return;

            int rewrote = 0;
            foreach (var c in McpClientRegistry.All)
            {
                try
                {
                    if (!c.IsInstalled) continue;
                    var before = c.Status;
                    if (before == McpStatus.NotConfigured) continue;
                    var after = c.CheckStatus(attemptAutoRewrite: true);
                    if (before != after && after == McpStatus.Configured) rewrote++;
                }
                catch (System.Exception ex)
                {
                    McpLog.Warn($"[StartupConfigRewrite] {c.DisplayName} failed: {ex.Message}");
                }
            }
            if (rewrote > 0)
                McpLog.Info($"[StartupConfigRewrite] refreshed {rewrote} client config(s).");
        }
    }
}
