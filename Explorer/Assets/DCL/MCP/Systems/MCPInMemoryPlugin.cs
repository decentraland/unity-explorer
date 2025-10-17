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
using System.Threading.Tasks;
using DCL.Chat.History;
using DCL.MCP.Host;
using ChatMessage = DCL.Chat.History.ChatMessage;
using Newtonsoft.Json.Linq;
using DCL.MCP.Handlers;
using ModelContextProtocol;
using System.Text.Json;
using Newtonsoft.Json;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Tool = UnityEditor.Tool;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

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
        private bool subscribedToChat;

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

            // Handlers (reuse existing game logic)
            var cameraHandler = new MCPCameraHandler(globalWorld);
            var screenshotHandler = new MCPScreenshotHandler(globalWorld);

            var options = new McpServerOptions
            {
                ServerInfo = new Implementation { Name = "MyServer", Version = "1.0.0" },
                Handlers = new McpServerHandlers
                {
                    ListToolsHandler = (request, cancellationToken) =>
                        new ValueTask<ListToolsResult>(new ListToolsResult
                        {
                            Tools = new ModelContextProtocol.Protocol.Tool[]
                            {
                                new ()
                                {
                                    Name = "echo",
                                    Description = "Echoes the input back to the client.",
                                    InputSchema = JsonSerializer.Deserialize<JsonElement>(@"{
  ""type"": ""object"",
  ""properties"": {
    ""message"": {
      ""type"": ""string"",
      ""description"": ""The input to echo back""
    }
  },
  ""required"": [""message""]
}"),
                                },
                            },
                        }),

                    CallToolHandler = (request, cancellationToken) =>
                    {
                        if (request.Params?.Name == "echo")
                        {
                            if (request.Params.Arguments?.TryGetValue("message", out JsonElement message) != true) { throw new Exception($"{nameof(McpErrorCode.InvalidParams)} : Missing required argument 'message'"); }

                            return new ValueTask<CallToolResult>(new CallToolResult
                            {
                                Content = new ContentBlock[] { new TextContentBlock { Text = $"Echo: {message}", Type = "text" } },
                            });
                        }

                        throw new Exception($"{nameof(McpErrorCode.InvalidRequest)} : Unknown tool: '{request.Params?.Name}'");
                    },
                },
            };

            server = McpServer.Create(
                new StreamServerTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream()),
                options
            );

            _ = server.RunAsync(ct);

            client = await McpClient.CreateAsync(
                new StreamClientTransport(clientToServerPipe.Writer.AsStream(), serverToClientPipe.Reader.AsStream()), cancellationToken: ct);

            // Create Host
            host = new MCPHost(client, server, model);
            host.AskGeminiToCallToolAsync(CancellationToken.None).Forget();

            // Подписываемся на чат, чтобы перехватывать сообщения с @ai
            TrySubscribeToChat();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose()
        {
            server?.DisposeAsync();
            client?.DisposeAsync();

            // Отписка от чата
            if (subscribedToChat && chatMessagesBus != null)
            {
                try { chatMessagesBus.MessageAdded -= OnChatMessageAdded; }
                catch
                { /* ignore */
                }

                subscribedToChat = false;
            }
        }

        private void TrySubscribeToChat()
        {
            if (subscribedToChat) return;
            if (chatMessagesBus == null) return;

            try
            {
                chatMessagesBus.MessageAdded += OnChatMessageAdded;
                subscribedToChat = true;
            }
            catch
            {
                // прототип: молча игнорируем
            }
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channel, ChatChannel.ChatChannelType type, ChatMessage message)
        {
            // Игнорируем сообщения от ИИ, чтобы не зациклиться
            if (message.IsSystemMessage) return;

            string text = message.Message ?? string.Empty;
            const string mention = "@ai";

            if (!text.Contains(mention, StringComparison.OrdinalIgnoreCase))
                return;

            // Вырезаем @ai и пробелы
            string prompt = text;
            int idx = prompt.IndexOf(mention, StringComparison.OrdinalIgnoreCase);

            if (idx >= 0)
                prompt = prompt.Remove(idx, mention.Length);

            prompt = prompt.Trim();

            if (string.IsNullOrWhiteSpace(prompt))
                return;

            // MCP flow: планируем и вызываем MCP tool через LLM и публикуем результат
            UniTask.Void(async () =>
            {
                string aiText = null;

                try
                {
                    (string toolName, Dictionary<string, object?> args, CallToolResult result, string raw)? plan = await host.PlanAndCallToolAsync(prompt, CancellationToken.None);

                    if (plan == null)
                    {
                        string reply = await host.AskLLMAsync(prompt, CancellationToken.None);
                        if (!string.IsNullOrWhiteSpace(reply))
                            aiText = "AI: " + reply;
                    }
                    else
                    {
                        string toolName = plan.Value.toolName;
                        aiText = "AI: Выполнил " + toolName;
                    }
                }
                catch
                { /* ignore */
                }

                if (string.IsNullOrWhiteSpace(aiText)) return;

                try { chatMessagesBus?.Send(ChatChannel.NEARBY_CHANNEL, aiText, "MCP", string.Empty); }
                catch
                { /* ignore */
                }
            });
        }
    }
}
