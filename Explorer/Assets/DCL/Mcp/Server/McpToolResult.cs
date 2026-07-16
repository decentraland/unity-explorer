using Newtonsoft.Json.Linq;
using System;

namespace DCL.Mcp.Protocol
{
    /// <summary>
    ///     The payload of a tools/call result: a content array of text/image items,
    ///     with expected failures flagged via isError instead of JSON-RPC errors.
    /// </summary>
    public readonly struct McpToolResult
    {
        public readonly JObject Payload;

        private McpToolResult(JObject payload)
        {
            Payload = payload;
        }

        public static McpToolResult Text(string text) =>
            new (new JObject
            {
                ["content"] = new JArray { TextItem(text) },
            });

        public static McpToolResult Error(string message) =>
            new (new JObject
            {
                ["content"] = new JArray { TextItem(message) },
                ["isError"] = true,
            });

        public static McpToolResult Image(byte[] imageBytes, string mimeType, string caption) =>
            new (new JObject
            {
                ["content"] = new JArray
                {
                    new JObject
                    {
                        ["type"] = "image",
                        ["data"] = Convert.ToBase64String(imageBytes),
                        ["mimeType"] = mimeType,
                    },
                    TextItem(caption),
                },
            });

        private static JObject TextItem(string text) =>
            new ()
            {
                ["type"] = "text",
                ["text"] = text,
            };
    }
}
