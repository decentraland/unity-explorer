using Cysharp.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DCL.MCP.Host
{
    public class MCPClient : MonoBehaviour
    {
        [ContextMenu("TEST AI")]
        public void Test() =>
            TestAsync().Forget();

        public async UniTask TestAsync()
        {
            // Абсолютный путь к вашему TS MCP серверу
            string serverEntry = @"c:\\DCL\\MCPServers\\explorer-mcp-server\\build\\index.js";

            var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "explorer-mcp-server",
                Command = "node",
                Arguments = new[] { serverEntry },
            });

            McpClient client = await McpClient.CreateAsync(clientTransport);

            // Tools
            IList<McpClientTool> tools = await client.ListToolsAsync(cancellationToken: CancellationToken.None);
            Debug.Log($"[MCP] Tools count: {tools.Count}");

            foreach (McpClientTool tool in tools) Debug.Log($"[MCP] Tool: {tool.Name} - {tool.Description}");

            // Resource templates (если сервер их объявляет)
            IList<McpClientResourceTemplate> templates = await client.ListResourceTemplatesAsync(CancellationToken.None);
            int templatesCount = templates.Count();

            Debug.Log($"[MCP] Resource templates: {templatesCount}");

            foreach (McpClientResourceTemplate t in templates) Debug.Log($"[MCP] Template: {t.Name} -> {t.UriTemplate}");

            // Resources listing
            IList<McpClientResource> resources = await client.ListResourcesAsync(CancellationToken.None);
            Debug.Log($"[MCP] Resources: {resources.Count}");

            foreach (McpClientResource r in resources) Debug.Log($"[MCP] Resource: {r.Name} -> {r.Uri}");

            // Пробуем прочитать первый доступный ресурс, если есть
            if (resources.Count > 0)
            {
                ReadResourceResult read = await client.ReadResourceAsync(resources[0].Uri!, CancellationToken.None);
                string text = read.Contents?.OfType<TextResourceContents>().FirstOrDefault()?.Text;

                if (!string.IsNullOrEmpty(text))
                    Debug.Log($"[MCP] Resource content: {text}");
            }
        }
    }
}
