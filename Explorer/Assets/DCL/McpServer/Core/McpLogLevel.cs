namespace DCL.McpServer.Core
{
    /// <summary>
    ///     RFC 5424 severities, as the MCP logging utility names them (the "level" field of a
    ///     notifications/message). Ordered low-to-high so a subscription's minimum filters by comparison.
    /// </summary>
    public enum McpLogLevel
    {
        Debug = 0,
        Info = 1,
        Notice = 2,
        Warning = 3,
        Error = 4,
        Critical = 5,
        Alert = 6,
        Emergency = 7,
    }

    public static class McpLogLevelExtensions
    {
        /// <summary>The lowercase wire name the MCP spec uses.</summary>
        public static string Wire(this McpLogLevel level) =>
            level switch
            {
                McpLogLevel.Debug => "debug",
                McpLogLevel.Info => "info",
                McpLogLevel.Notice => "notice",
                McpLogLevel.Warning => "warning",
                McpLogLevel.Error => "error",
                McpLogLevel.Critical => "critical",
                McpLogLevel.Alert => "alert",
                McpLogLevel.Emergency => "emergency",
                _ => "info",
            };

        /// <summary>Parses a spec level name; false for anything unrecognised.</summary>
        public static bool TryParse(string? name, out McpLogLevel level)
        {
            switch (name)
            {
                case "debug": level = McpLogLevel.Debug; return true;
                case "info": level = McpLogLevel.Info; return true;
                case "notice": level = McpLogLevel.Notice; return true;
                case "warning": level = McpLogLevel.Warning; return true;
                case "error": level = McpLogLevel.Error; return true;
                case "critical": level = McpLogLevel.Critical; return true;
                case "alert": level = McpLogLevel.Alert; return true;
                case "emergency": level = McpLogLevel.Emergency; return true;
                default: level = McpLogLevel.Info; return false;
            }
        }
    }
}
