using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Chat.MessageBus;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Profiles;
using ECS.SceneLifeCycle;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Mscc.GenerativeAI;
using System;
using System.IO.Pipelines;
using System.Threading;

namespace DCL.MCP
{
    /// <summary>
    ///     Глобальный плагин для автоматической инициализации MCP WebSocket сервера.
    ///     Запускается при старте приложения и живёт на протяжении всей сессии.
    ///     Отвечает только за инициализацию и регистрацию обработчиков команд.
    /// </summary>
    public class MCPInMemoryPlugin : IDCLGlobalPluginWithoutSettings
    {
        private const string GEMINI_API_KEY = "AIzaSyANdi9MKBS1xb73K-A-1orCwGTeNLtbrRQ";
        private const string MODEL_NAME = "gemini-2.5-flash";

        private readonly World globalWorld;
        private readonly IScenesCache scenesCache;
        private readonly IProfileRepository profileRepository;
        private readonly IChatMessagesBus chatMessagesBus;

        private McpServer server;
        private McpClient client;
        private Pipe clientToServerPipe;
        private Pipe serverToClientPipe;
        private GoogleAI googleAI;
        private GenerativeModel model;
        private MCPHost host;

        public MCPInMemoryPlugin(World globalWorld, IScenesCache scenesCache, IProfileRepository profileRepository, IChatMessagesBus chatMessagesBus)
        {
            this.globalWorld = globalWorld;
            this.scenesCache = scenesCache;
            this.profileRepository = profileRepository;
            this.chatMessagesBus = chatMessagesBus;
        }

        public async UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            // Create LLM
            googleAI = new GoogleAI(string.IsNullOrWhiteSpace(GEMINI_API_KEY) ? Environment.GetEnvironmentVariable("GEMINI_API_KEY") : GEMINI_API_KEY);
            model = string.IsNullOrWhiteSpace(MODEL_NAME) ? googleAI.GenerativeModel() : googleAI.GenerativeModel(MODEL_NAME);

            // Create MCP
            clientToServerPipe = new Pipe();
            serverToClientPipe = new Pipe();

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

            // Create Host
            host = new MCPHost(client, server, model);
            host.AskGeminiToCallToolAsync(CancellationToken.None).Forget();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            server?.DisposeAsync();
            client?.DisposeAsync();
        }
    }
}
