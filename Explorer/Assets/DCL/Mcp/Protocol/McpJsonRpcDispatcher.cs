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

        private const string SERVER_NAME = "dcl-unity-explorer";

        private const int PARSE_ERROR = -32700;
        private const int INVALID_REQUEST = -32600;
        private const int METHOD_NOT_FOUND = -32601;
        private const int INVALID_PARAMS = -32602;

        private readonly McpToolsRegistry tools;
        private readonly string serverVersion;

        // Lets an agent orchestrating several Explorer instances confirm which process answers on this port.
        private readonly int processId = System.Diagnostics.Process.GetCurrentProcess().Id;

        public McpJsonRpcDispatcher(McpToolsRegistry tools, string serverVersion)
        {
            this.tools = tools;
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
            catch (JsonException) { return JsonRpcEnvelope.Error(null, PARSE_ERROR, "Parse error"); }

            JToken? id = request["id"];
            string? method = request["method"]?.Value<string>();

            if (string.IsNullOrEmpty(method))
                return id == null ? null : JsonRpcEnvelope.Error(id, INVALID_REQUEST, "Invalid request: missing method");

            // Messages without an id are notifications ("notifications/initialized" et al.) and get no response.
            if (id == null)
                return null;

            return method switch
                   {
                       "initialize" => JsonRpcEnvelope.Result(id, InitializeResult()),
                       "ping" => JsonRpcEnvelope.Result(id, new JObject()),
                       "tools/list" => JsonRpcEnvelope.Result(id, tools),
                       "tools/call" => await CallToolAsync(id, request["params"] as JObject, ct),
                       _ => JsonRpcEnvelope.Error(id, METHOD_NOT_FOUND, $"Method not found: {method}")
                   };
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

        private async UniTask<string?> CallToolAsync(JToken id, JObject? callParams, CancellationToken ct)
        {
            string? toolName = callParams?["name"]?.Value<string>();

            if (!tools.TryGet(toolName, out IMcpTool? tool))
                return JsonRpcEnvelope.Error(id, INVALID_PARAMS, $"Unknown tool: {toolName ?? "<missing>"}");

            JObject arguments = callParams?["arguments"] as JObject ?? new JObject();

            try
            {
                McpToolResult result = await tool.ExecuteAsync(arguments, ct);
                return JsonRpcEnvelope.Result(id, result.Payload);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.MCP);
                return JsonRpcEnvelope.Result(id, McpToolResult.Error($"Tool '{toolName}' failed: {e.Message}").Payload);
            }
        }

        private static class JsonRpcEnvelope
        {
            public static string Result(JToken id, JToken result) =>
                new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = result,
                }.ToString(Formatting.None);

            public static string Error(JToken? id, int code, string message) =>
                new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id ?? JValue.CreateNull(),
                    ["error"] = new JObject
                    {
                        ["code"] = code,
                        ["message"] = message,
                    },
                }.ToString(Formatting.None);
        }
    }
}
