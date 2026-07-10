using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Mcp.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.Mcp.Protocol
{
    /// <summary>
    ///     Routes JSON-RPC 2.0 messages of the MCP Streamable HTTP transport to the tool registry.
    ///     Only the tools capability is implemented; resources and prompts are not declared.
    /// </summary>
    public class McpJsonRpcDispatcher
    {
        private readonly McpToolRegistry toolRegistry;
        private readonly string serverVersion;

        // Lets an agent orchestrating several Explorer instances confirm which process answers on this port.
        private readonly int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

        public McpJsonRpcDispatcher(McpToolRegistry toolRegistry, string serverVersion)
        {
            this.toolRegistry = toolRegistry;
            this.serverVersion = serverVersion;
        }

        /// <summary>
        ///     Returns the serialized JSON-RPC response, or null when the message is a notification
        ///     (or a response relayed by the client) and no reply must be sent.
        /// </summary>
        public async UniTask<string?> DispatchAsync(string requestJson, CancellationToken ct)
        {
            JObject request;

            try { request = JObject.Parse(requestJson); }
            catch (JsonException) { return Serialize(JsonRpc.Error(null, McpConstants.PARSE_ERROR, "Parse error")); }

            JToken? id = request["id"];
            string? method = request["method"]?.Value<string>();

            if (string.IsNullOrEmpty(method))
                return id == null ? null : Serialize(JsonRpc.Error(id, McpConstants.INVALID_REQUEST, "Invalid request: missing method"));

            // Messages without an id are notifications ("notifications/initialized" et al.) and get no response.
            if (id == null)
                return null;

            switch (method)
            {
                case "initialize":
                    return Serialize(JsonRpc.Result(id, InitializeResult()));
                case "ping":
                    return Serialize(JsonRpc.Result(id, new JObject()));
                case "tools/list":
                    return Serialize(JsonRpc.Result(id, toolRegistry.ToolsListResult));
                case "tools/call":
                    return await CallToolAsync(id, request["params"] as JObject, ct);
                default:
                    return Serialize(JsonRpc.Error(id, McpConstants.METHOD_NOT_FOUND, $"Method not found: {method}"));
            }
        }

        private async UniTask<string?> CallToolAsync(JToken id, JObject? callParams, CancellationToken ct)
        {
            string? toolName = callParams?["name"]?.Value<string>();

            if (string.IsNullOrEmpty(toolName) || !toolRegistry.TryGet(toolName, out IMcpTool? tool))
                return Serialize(JsonRpc.Error(id, McpConstants.INVALID_PARAMS, $"Unknown tool: {toolName ?? "<missing>"}"));

            JObject arguments = callParams?["arguments"] as JObject ?? new JObject();

            try
            {
                McpToolResult result = await tool.ExecuteAsync(arguments, ct);
                return Serialize(JsonRpc.Result(id, result.Payload));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.MCP);
                return Serialize(JsonRpc.Result(id, McpToolResult.Error($"Tool '{toolName}' failed: {e.Message}").Payload));
            }
        }

        private JObject InitializeResult() =>
            new ()
            {
                ["protocolVersion"] = McpConstants.PROTOCOL_VERSION,
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject
                {
                    ["name"] = McpConstants.SERVER_NAME,
                    ["version"] = serverVersion,
                    ["pid"] = processId,
                },
            };

        private static string Serialize(JObject response) =>
            response.ToString(Formatting.None);
    }
}
