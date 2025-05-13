namespace UnityMcpBridge.Editor.Models
{
    public class McpClient
    {
        public string name;
        public string windowsConfigPath;
        public string linuxConfigPath;
        public McpTypes mcpType;
        public string configStatus;
        public McpStatus status = McpStatus.NotConfigured;

        // Helper method to convert the enum to a display string
        public string GetStatusDisplayString()
        {string result;
            switch (status)
            {
                case McpStatus.NotConfigured:
                    result = "Not Configured";
                    break;
                case McpStatus.Configured:
                    result = "Configured";
                    break;
                case McpStatus.Running:
                    result = "Running";
                    break;
                case McpStatus.Connected:
                    result = "Connected";
                    break;
                case McpStatus.IncorrectPath:
                    result = "Incorrect Path";
                    break;
                case McpStatus.CommunicationError:
                    result = "Communication Error";
                    break;
                case McpStatus.NoResponse:
                    result = "No Response";
                    break;
                case McpStatus.UnsupportedOS:
                    result = "Unsupported OS";
                    break;
                case McpStatus.MissingConfig:
                    result = "Missing UnityMCP Config";
                    break;
                case McpStatus.Error:
                    result = configStatus.StartsWith("Error:") ? configStatus : "Error";
                    break;
                default:
                    result = "Unknown";
                    break;
            }
            return result;
        }

        // Helper method to set both status enum and string for backward compatibility
        public void SetStatus(McpStatus newStatus, string errorDetails = null)
        {
            status = newStatus;

            if (newStatus == McpStatus.Error && !string.IsNullOrEmpty(errorDetails))
            {
                configStatus = $"Error: {errorDetails}";
            }
            else
            {
                configStatus = GetStatusDisplayString();
            }
        }
    }
}
