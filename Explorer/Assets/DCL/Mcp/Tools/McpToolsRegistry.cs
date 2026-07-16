using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Mcp.Tools
{
    public class McpToolsRegistry
    {
        private readonly Dictionary<string, IMcpTool> tools = new ();

        private JObject toolsList = null!;

        /// <summary>Lets the built registry stand in directly for its tools/list payload.</summary>
        public static implicit operator JObject(McpToolsRegistry registry) =>
            registry.toolsList;

        public McpToolsRegistry Register(IMcpTool tool)
        {
            tools.Add(tool.Name, tool);
            return this;
        }

        public McpToolsRegistry Build()
        {
            var toolsArray = new JArray();

            foreach (IMcpTool tool in tools.Values)
            {
                toolsArray.Add(new JObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["inputSchema"] = JObject.Parse(tool.InputSchemaJson),
                });
            }

            toolsList = new JObject { ["tools"] = toolsArray };
            return this;
        }

        public bool TryGet(string name, [NotNullWhen(true)] out IMcpTool? tool) =>
            tools.TryGetValue(name, out tool);
    }
}
