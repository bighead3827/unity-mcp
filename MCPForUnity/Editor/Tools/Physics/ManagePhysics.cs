using System;
using Newtonsoft.Json.Linq;
using MCPForUnity.Editor.Helpers;

namespace MCPForUnity.Editor.Tools.Physics
{
    [McpForUnityTool("manage_physics", AutoRegister = false, Group = "core")]
    public static class ManagePhysics
    {
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);
            string action = p.Get("action")?.ToLowerInvariant();

            if (string.IsNullOrEmpty(action))
                return new ErrorResponse("'action' parameter is required.");

            try
            {
                switch (action)
                {
                    // --- Health check ---
                    case "ping":
                        return PhysicsSettingsOps.Ping(@params);

                    // --- Settings actions ---
                    case "get_settings":
                        return PhysicsSettingsOps.GetSettings(@params);
                    case "set_settings":
                        return PhysicsSettingsOps.SetSettings(@params);

                    default:
                        return new ErrorResponse(
                            $"Unknown action: '{action}'. Valid actions: ping, "
                            + "get_settings, set_settings.");
                }
            }
            catch (Exception ex)
            {
                McpLog.Error($"[ManagePhysics] Action '{action}' failed: {ex}");
                return new ErrorResponse($"Error in action '{action}': {ex.Message}");
            }
        }
    }
}
