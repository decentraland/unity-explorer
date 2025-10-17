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
using DCL.Chat.History;
using DCL.MCP.Handlers;
using DCL.MCP.Host;
using ChatMessage = DCL.Chat.History.ChatMessage;
using Newtonsoft.Json.Linq;
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

            server = McpServer.Create(
                new StreamServerTransport(clientToServerPipe.Reader.AsStream(), serverToClientPipe.Writer.AsStream()),
                new McpServerOptions
                {
                    ToolCollection = new McpServerPrimitiveCollection<McpServerTool>
                    {
                        // Echo tool: также пишет в игровой чат с префиксом [AI] :
                        McpServerTool.Create(new Func<string, string>(arg =>
                        {
                            string text = "[AI] : " + (arg ?? string.Empty);
                            chatMessagesBus?.Send(ChatChannel.NEARBY_CHANNEL, text, "MCP", string.Empty);
                            return $"Echo: {arg}";
                        }), new McpServerToolCreateOptions { Name = "Echo" }),

                        // Camera tools (JSON string in 'arg')
                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleStartCameraControlAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "start_inworld_camera_control" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleStopCameraControlAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "stop_inworld_camera_control" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleSetCameraPositionAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "set_inworld_camera_position" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleSetCameraRotationAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "set_inworld_camera_rotation" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleSetCameraLookAtAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "set_inworld_camera_look_at" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleSetCameraLookAtPlayerAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "set_inworld_camera_look_at_player" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleSetCameraFOVAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "set_inworld_camera_fov" }),

                        McpServerTool.Create(new Func<string, object>(json =>
                        {
                            JObject j = string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
                            return cameraHandler.HandleGetCameraStateAsync(j).AsTask().Result;
                        }), new McpServerToolCreateOptions { Name = "get_inworld_camera_state" }),
                    },
                });

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
