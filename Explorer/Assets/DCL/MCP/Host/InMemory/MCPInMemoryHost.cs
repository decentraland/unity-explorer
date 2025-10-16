using Cysharp.Threading.Tasks;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using UnityEngine;

public class MCPInMemoryHost : MonoBehaviour
{
    // [SerializeField] private MCPInMemoryServer server;
    // [SerializeField] private MCPInMemoryClient client;

    private readonly Pipe clientToServerPipe = new ();
    private readonly Pipe serverToClientPipe = new ();

    private McpServer server;
    private McpClient client;
    private IList<McpClientTool> tools;

    private CancellationTokenSource tokenSource = new ();

    [ContextMenu("CREATE")]
    private void Create()
    {
        tokenSource.Cancel();
        tokenSource.Dispose();
        tokenSource = new CancellationTokenSource();

        CreateAsync(tokenSource.Token).Forget();
    }

    private async UniTask CreateAsync(CancellationToken ct)
    {
        if (server != null) await server.DisposeAsync();
        if (client != null) await client.DisposeAsync();
        tools?.Clear();

        server = McpServer.Create(
            new StreamServerTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream()),
            new McpServerOptions
            {
                ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                {
                    McpServerTool.Create(new Func<string, string>(arg => $"Echo: {arg}"), new McpServerToolCreateOptions { Name = "Echo" }),
                },
            });

        _ = server.RunAsync(ct);

        client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServerPipe.Writer.AsStream(), serverToClientPipe.Reader.AsStream()), cancellationToken: ct);

        tools = await client.ListToolsAsync(cancellationToken: ct);
        foreach (McpClientTool tool in tools) Debug.Log($"Tool Name: {tool.Name}");
    }

    [ContextMenu("CALL ECHO")]
    private void CallEcho()
    {
        CallEchoAsync(tokenSource.Token).Forget();
    }

    private async UniTask CallEchoAsync(CancellationToken ct)
    {
        McpClientTool echo = tools.First(t => t.Name == "Echo");

        Debug.Log(await echo.InvokeAsync(new AIFunctionArguments
        {
            ["arg"] = "Hello World",
        }, ct));
    }
}
