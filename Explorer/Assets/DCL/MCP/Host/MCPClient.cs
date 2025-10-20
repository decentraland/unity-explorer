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
        [SerializeField] private string serverEntry = @"c:\\DCL\\MCPServers\\explorer-mcp-server\\build\\index.js";
        public McpClient _client;

        [ContextMenu("TEST AI")]
        public void Test() =>
            TestAsync().Forget();

        public async UniTask TestAsync()
        {
            McpClient client = await EnsureConnectedAsync();

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

        public async UniTask<McpClient> EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            if (_client != null)
                return _client;

            var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "explorer-mcp-server",
                Command = "node",
                Arguments = new[] { serverEntry },
            });

            _client = await McpClient.CreateAsync(clientTransport, cancellationToken: cancellationToken);
            return _client;
        }

        public async UniTask<IList<McpClientTool>> GetToolsAsync(CancellationToken cancellationToken = default)
        {
            McpClient client = await EnsureConnectedAsync(cancellationToken);
            return await client.ListToolsAsync(cancellationToken: cancellationToken);
        }

        public async UniTask<CallToolResult> InvokeToolAsync(string toolName, IDictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            McpClient client = await EnsureConnectedAsync(cancellationToken);
            return await client.CallToolAsync(toolName, new Dictionary<string, object?>(arguments), cancellationToken: cancellationToken);
        }
    }
}
