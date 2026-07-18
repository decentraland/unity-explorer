using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.McpServer.Core
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

        /// <summary>
        ///     A text result whose body is <paramref name="payload" /> rendered as indented JSON. Centralizes the
        ///     Formatting.Indented serialization so tools never render JSON differently or forget the formatting.
        /// </summary>
        public static McpToolResult Json(JObject payload) =>
            Text(payload.ToString(Formatting.Indented));

        /// <summary>
        ///     A result that surfaces <paramref name="structured" /> both as structuredContent (validated against the
        ///     tool's outputSchema) and as its indented-JSON text duplicate — the spec requires the text mirror so
        ///     clients without structured support still read the result. This is the common path; it serializes the
        ///     mirror itself so the two copies cannot drift.
        /// </summary>
        public static McpToolResult JsonWithStructured(JObject structured) =>
            TextWithStructured(structured.ToString(Formatting.Indented), structured);

        /// <summary>
        ///     Like <see cref="JsonWithStructured" /> but with an explicit <paramref name="text" /> mirror, for the rare
        ///     case where the human-readable text is deliberately not the raw serialization of <paramref name="structured" />.
        /// </summary>
        public static McpToolResult TextWithStructured(string text, JObject structured) =>
            new (new JObject
            {
                ["content"] = new JArray { TextItem(text) },
                ["structuredContent"] = structured,
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
