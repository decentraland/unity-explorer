using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DCL.Mcp.Tools
{
    public class McpToolRegistry
    {
        private readonly Dictionary<string, IMcpTool> tools = new ();

        private JObject? cachedToolsListResult;

        /// <summary>
        ///     The tools/list result, built once and reused for every request.
        /// </summary>
        public JObject ToolsListResult => cachedToolsListResult ??= BuildToolsListResult();

        public void Register(IMcpTool tool)
        {
            tools.Add(tool.Name, tool);
            cachedToolsListResult = null;
        }

        public bool TryGet(string name, [NotNullWhen(true)] out IMcpTool? tool) =>
            tools.TryGetValue(name, out tool);

        private JObject BuildToolsListResult()
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

            return new JObject { ["tools"] = toolsArray };
        }
    }
}
