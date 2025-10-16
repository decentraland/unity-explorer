using Cysharp.Threading.Tasks;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.IO;
using UnityEngine;

public class MCPInMemoryClient : MonoBehaviour
{
    [SerializeField] private MCPInMemoryServer server;
    private McpClient _client;

    public async UniTask<McpClient> EnsureConnectedAsync()
    {
        if (_client != null)
            return _client;

        if (server == null)
        {
            Debug.LogError("[InMemory MCP] Server reference is not set on client");
            return null;
        }

        await server.StartServerAsync();

        (Stream clientWrite, Stream clientRead) = server.GetClientStreams();
        _client = await McpClient.CreateAsync(new StreamClientTransport(clientWrite, clientRead));
        return _client;
    }

    public async UniTask<CallToolResult> EchoAsync(string message)
    {
        McpClient client = await EnsureConnectedAsync();
        if (client == null) throw new InvalidOperationException("Client not connected");

        return await client.CallToolAsync("Echo", new System.Collections.Generic.Dictionary<string, object?>
        {
            ["message"] = message,
        });
    }
}
