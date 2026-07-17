using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.McpServer.Core
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
            var routable = ParseRoutableRequest(requestJson, out string? earlyResponse);
            if (routable == null)
                return earlyResponse;

            JToken id = routable.Value.id;

            return routable.Value.method switch
                   {
                       "initialize" => JsonRpcEnvelope.Result(id, InitializeResult(routable.Value.callParams)),
                       "ping" => JsonRpcEnvelope.Result(id, new JObject()),
                       "tools/list" => JsonRpcEnvelope.Result(id, tools),
                       "tools/call" => await CallToolAsync(id,
                           toolName: routable.Value.callParams?["name"]?.Value<string>(),
                           arguments: routable.Value.callParams?["arguments"] as JObject ?? new JObject(),
                           ct),
                       _ => JsonRpcEnvelope.Error(id, METHOD_NOT_FOUND, $"Method not found: {routable.Value.method}")
                   };
        }

        /// <summary>
        ///     Parses the raw message and returns the id, method and params to route on.
        ///     Returns null when there is nothing to route: <paramref name="earlyResponse" /> then
        ///     carries the reply to send back (a JSON-RPC error) or null for a notification that gets no response.
        /// </summary>
        private static (JToken id, string method, JObject? callParams)? ParseRoutableRequest(string requestJson, out string? earlyResponse)
        {
            earlyResponse = null;

            JToken parsed;
            try { parsed = JToken.Parse(requestJson); }
            catch (JsonException)
            {
                earlyResponse = JsonRpcEnvelope.Error(null, PARSE_ERROR, "Parse error");
                return null;
            }

            // -32700 is reserved for unparseable JSON; well-formed JSON that is not a JSON-RPC object
            // (a bare array, number or string) is a valid document but an invalid request: -32600.
            if (parsed is not JObject request)
            {
                earlyResponse = JsonRpcEnvelope.Error(null, INVALID_REQUEST, "Invalid request: expected a JSON-RPC object");
                return null;
            }

            JToken? id = request["id"];
            string? method = request["method"]?.Value<string>();

            if (string.IsNullOrEmpty(method))
            {
                earlyResponse = id == null ? null : JsonRpcEnvelope.Error(id, INVALID_REQUEST, "Invalid request: missing method");
                return null;
            }

            // Messages without an id are notifications ("notifications/initialized" et al.) and get no response.
            if (id == null)
                return null;

            return (id, method, request["params"] as JObject);
        }

        private JObject InitializeResult(JObject? initializeParams)
        {
            // We implement a single MCP revision. Per the handshake rules we answer with our version regardless,
            // but a client pinned to a different revision may abort on a strict mismatch — surface it so the
            // otherwise-silent interop failure is diagnosable from the server logs.
            string? requestedVersion = initializeParams?["protocolVersion"]?.Value<string>();

            if (!string.IsNullOrEmpty(requestedVersion) && requestedVersion != PROTOCOL_VERSION)
                ReportHub.LogWarning(ReportCategory.MCP, $"MCP client requested protocol version '{requestedVersion}', server responding with '{PROTOCOL_VERSION}'");

            return new JObject
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
        }

        private async UniTask<string?> CallToolAsync(JToken id, string? toolName, JObject arguments, CancellationToken ct)
        {
            if (!tools.TryGet(toolName, out IMcpTool? tool))
                return JsonRpcEnvelope.Error(id, INVALID_PARAMS, $"Unknown tool: {toolName ?? "<missing>"}");

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
