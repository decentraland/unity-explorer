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
        /// <summary>Version of the MCP specification this server implements, declared in the initialize handshake.</summary>
        public const string PROTOCOL_VERSION = "2025-06-18";

        private const string SERVER_NAME = "decentraland-explorer";

        private const int PARSE_ERROR = -32700;
        private const int INVALID_REQUEST = -32600;
        private const int METHOD_NOT_FOUND = -32601;
        private const int INVALID_PARAMS = -32602;

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
            catch (JsonException) { return Serialize(JsonRpc.Error(null, PARSE_ERROR, "Parse error")); }

            JToken? id = request["id"];
            string? method = request["method"]?.Value<string>();

            if (string.IsNullOrEmpty(method))
                return id == null ? null : Serialize(JsonRpc.Error(id, INVALID_REQUEST, "Invalid request: missing method"));

            // Messages without an id are notifications ("notifications/initialized" et al.) and get no response.
            if (id == null)
                return null;

            return method switch
                   {
                       "initialize" => Serialize(JsonRpc.Result(id, InitializeResult())),
                       "ping" => Serialize(JsonRpc.Result(id, new JObject())),
                       "tools/list" => Serialize(JsonRpc.Result(id, toolRegistry.ToolsList)),
                       "tools/call" => await CallToolAsync(id, request["params"] as JObject, ct),
                       _ => Serialize(JsonRpc.Error(id, METHOD_NOT_FOUND, $"Method not found: {method}"))
                   };
        }

        private async UniTask<string?> CallToolAsync(JToken id, JObject? callParams, CancellationToken ct)
        {
            string? toolName = callParams?["name"]?.Value<string>();

            if (string.IsNullOrEmpty(toolName) || !toolRegistry.TryGet(toolName, out IMcpTool? tool))
                return Serialize(JsonRpc.Error(id, INVALID_PARAMS, $"Unknown tool: {toolName ?? "<missing>"}"));

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
                ["protocolVersion"] = PROTOCOL_VERSION,
                ["capabilities"] = new JObject { ["tools"] = new JObject() },
                ["serverInfo"] = new JObject
                {
                    ["name"] = SERVER_NAME,
                    ["version"] = serverVersion,
                    ["pid"] = processId,
                },
            };

        private static string Serialize(JObject response) =>
            response.ToString(Formatting.None);
    }
}
