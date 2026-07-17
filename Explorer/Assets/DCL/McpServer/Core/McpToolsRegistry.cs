using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.McpServer.Core
{
    public class McpToolsRegistry
    {
        private readonly Dictionary<string, IMcpTool> tools = new ();
        private JObject toolsList = null!;

        /// <summary>
        ///     Lets the built registry stand in directly for its tools/list payload. Returns a detached clone:
        ///     dispatched requests run concurrently on the thread pool, and attaching the shared instance to a
        ///     response envelope re-parents it in place, so handing out the same object would race on its parent/
        ///     sibling pointers during serialization.
        /// </summary>
        public static implicit operator JObject(McpToolsRegistry registry) =>
            (JObject)registry.toolsList.DeepClone();

        public McpToolsRegistry Add(IMcpTool tool)
        {
            tools.Add(tool.Name, tool);
            return this;
        }

        public McpToolsRegistry Build()
        {
            var toolsArray = new JArray();

            foreach (IMcpTool tool in tools.Values)
            {
                JObject inputSchema = tool.InputSchema;

                if (inputSchema == null || inputSchema["type"]?.Value<string>() != "object")
                    throw new InvalidOperationException($"MCP tool '{tool.Name}' produced an invalid input schema: expected a JSON Schema object (\"type\": \"object\"). Build it with McpInputSchema.");

                var entry = new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = inputSchema,
                    ["annotations"] = tool.Annotations.ToJObject(),
                };

                if (tool.OutputSchema != null)
                    entry["outputSchema"] = tool.OutputSchema;

                toolsArray.Add(entry);
            }

            toolsList = new JObject { ["tools"] = toolsArray };
            return this;
        }

        public bool TryGet(string? name, [NotNullWhen(true)] out IMcpTool? tool)
        {
            tool = null;

            if (string.IsNullOrEmpty(name))
                return false;

            return tools.TryGetValue(name, out tool);
        }
    }
}
