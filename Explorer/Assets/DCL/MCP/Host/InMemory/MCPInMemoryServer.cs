using Cysharp.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using UnityEngine;

public class MCPInMemoryServer : MonoBehaviour
{
    private Pipe _clientToServer;
    private Pipe _serverToClient;
    private McpServer _server;

    public (Pipe clientToServer, Pipe serverToClient) EnsurePipes()
    {
        if (_clientToServer == null || _serverToClient == null)
        {
            _clientToServer = new Pipe();
            _serverToClient = new Pipe();
        }

        return (_clientToServer, _serverToClient);
    }

    [ContextMenu("Start InMemory MCP Server")]
    public void StartServer() =>
        StartServerAsync().Forget();

    public async UniTask StartServerAsync()
    {
        try
        {
            (Pipe clientToServer, Pipe serverToClient) = EnsurePipes();

            if (_server != null) { await _server.DisposeAsync(); }

            _server = McpServer.Create(
                new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
                new McpServerOptions
                {
                    ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                    {
                        McpServerTool.Create(new Func<string, string>(arg => $"Echo: {arg}"), new McpServerToolCreateOptions { Name = "Echo" }),
                    },
                });

            Debug.Log("[InMemory MCP] Server starting...");
            _ = _server.RunAsync();
            Debug.Log("[InMemory MCP] Server started.");
        }
        catch (Exception e) { Debug.LogError($"[InMemory MCP] Server start error: {e.Message}"); }
    }

    public (System.IO.Stream clientWrite, System.IO.Stream clientRead) GetClientStreams()
    {
        (Pipe clientToServer, Pipe serverToClient) = EnsurePipes();
        return (clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream());
    }
}
