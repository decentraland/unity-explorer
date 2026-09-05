using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Core
{
    public class McpToolsRegistry
    {
        private readonly Dictionary<string, McpTool> tools = new ();
        private string toolsListJson = null!;

        /// <summary>
        ///     The tools/list payload, serialized once at Build(). Each dispatch wraps the shared immutable
        ///     JSON string in a fresh JRaw, so there is no shared JToken tree to clone or re-parent when
        ///     concurrent responses serialize on the thread pool.
        /// </summary>
        public JRaw ToolsListPayload() => new (toolsListJson);

        public McpToolsRegistry Add(McpTool tool)
        {
            tools.Add(tool.Name, tool);
            return this;
        }

        public McpToolsRegistry Build()
        {
            var toolsArray = new JArray();

            foreach (McpTool tool in tools.Values)
            {
                if (tool.OutputSchema != null && !IsObjectSchema(tool.OutputSchema))
                    throw new InvalidOperationException($"MCP tool '{tool.Name}' produced an invalid output schema: expected a JSON Schema object (\"type\": \"object\"). Build it with McpJsonSchema.");

                var entry = new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = tool.InputSchema,
                    ["annotations"] = tool.Annotations.ToJObject(),
                };

                if (tool.OutputSchema != null)
                    entry["outputSchema"] = tool.OutputSchema;

                toolsArray.Add(entry);
            }

            toolsListJson = new JObject { ["tools"] = toolsArray }.ToString(Formatting.None);
            return this;
        }

        private static bool IsObjectSchema(JObject schema) =>
            schema["type"]?.Value<string>() == "object";

        public bool TryGet(string? name, [NotNullWhen(true)] out McpTool? tool)
        {
            tool = null;

            if (string.IsNullOrEmpty(name))
                return false;

            return tools.TryGetValue(name, out tool);
        }
    }
}
