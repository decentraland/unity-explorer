using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Mcp.Tools
{
    public class McpToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> tools = new ();

        public JObject ToolsList { get; private set; } = new () { ["tools"] = new JArray() };

        public McpToolRegistry Register(IMcpTool tool)
        {
            tools.Add(tool.Name, tool);
            return this;
        }

        public McpToolRegistry Build()
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

            ToolsList = new JObject { ["tools"] = toolsArray };
            return this;
        }

        public bool TryGet(string name, [NotNullWhen(true)] out IMcpTool? tool) =>
            tools.TryGetValue(name, out tool);
    }
}
