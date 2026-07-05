using Newtonsoft.Json.Linq;

namespace DCL.Mcp.Protocol
{
    /// <summary>
    ///     Helpers to build JSON-RPC 2.0 response envelopes.
    /// </summary>
    public static class JsonRpc
    {
        public static JObject Result(JToken id, JToken result) =>
            new ()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result,
            };

        public static JObject Error(JToken? id, int code, string message) =>
            new ()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message,
                },
            };
    }
}
