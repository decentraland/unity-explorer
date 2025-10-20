using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.MCP.Host;
using DCL.Utilities;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Mscc.GenerativeAI.Microsoft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace DCL.MCP
{
    /// <summary>
    ///     Мост между игровым чатом и AI с MCP тулзами.
    ///     Прототип - быстро, грязно, статика, без секьюрити.
    /// </summary>
    public class MCPChatAIBridge : MonoBehaviour
    {
        [SerializeField] private string geminiApiKey;
        [SerializeField] private string modelName = "gemini-2.0-flash-exp";
        [SerializeField] private MCPClient mcpClient;

        private IChatClient geminiClient;
        private IList<McpClientTool> availableTools;
        private bool isProcessing;

        private void Start()
        {
            InitializeAsync().Forget();
        }

        private async UniTask InitializeAsync()
        {
            // Ждем пока ChatMessagesBus будет доступен
            var attempts = 0;

            while (MCPGlobalLocator.ChatMessagesBus == null && attempts < 50)
            {
                await UniTask.Delay(100);
                attempts++;
            }

            if (MCPGlobalLocator.ChatMessagesBus == null)
            {
                Debug.LogError("[MCPChatAI] ChatMessagesBus не доступен после ожидания");
                return;
            }

            // Подписываемся на сообщения чата
            if (MCPGlobalLocator.ChatMessagesBus is IChatMessagesBus chatBus)
            {
                chatBus.MessageAdded += OnChatMessageAdded;
                Debug.Log("[MCPChatAI] Подписались на события чата");
            }

            // Инициализируем Gemini клиента
            geminiClient = new GeminiChatClient(geminiApiKey, modelName);

            // Загружаем доступные тулзы из MCP
            try
            {
                availableTools = await mcpClient.GetToolsAsync();
                Debug.Log($"[MCPChatAI] Загружено {availableTools.Count} MCP тулз");
            }
            catch (Exception ex) { Debug.LogError($"[MCPChatAI] Ошибка загрузки тулз: {ex.Message}"); }
        }

        private void OnChatMessageAdded(ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType, DCL.Chat.History.ChatMessage message)
        {
            if (!message.Message.TrimStart().StartsWith("@ai ", StringComparison.OrdinalIgnoreCase))
                return;

            // Извлекаем запрос (убираем "@ai ")
            string query = message.Message.Substring(message.Message.IndexOf("@ai", StringComparison.OrdinalIgnoreCase) + 3).Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            Debug.Log($"[MCPChatAI] Получен запрос: {query}");

            // Обрабатываем запрос асинхронно
            ProcessAIQueryAsync(query, channelId, channelType).Forget();
        }

        private async UniTask ProcessAIQueryAsync(string query, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if (isProcessing)
            {
                SendChatMessage("MCP: Обрабатываю предыдущий запрос, подождите...", channelId, channelType);
                return;
            }

            isProcessing = true;

            try
            {
                if (geminiClient == null || availableTools == null)
                {
                    SendChatMessage("MCP: AI не инициализирован", channelId, channelType);
                    return;
                }

                var chatOptions = new ChatOptions
                {
                    Tools = availableTools.ToArray<AITool>(),
                };

                var messages = new List<ChatMessage>
                {
                    new (ChatRole.User, query),
                };

                // Первый запрос к AI
                ChatResponse response = await geminiClient.GetResponseAsync(messages, chatOptions);

                // Проверяем function calls
                IEnumerable<FunctionCallContent> functionCalls = response.Messages[0].Contents.OfType<FunctionCallContent>();

                if (functionCalls.Any())
                {
                    Debug.Log($"[MCPChatAI] AI вызывает {functionCalls.Count()} тулзу(тулз)");

                    // Добавляем ответ модели в историю
                    messages.Add(response.Messages[0]);

                    // Вызываем каждую тулзу
                    foreach (FunctionCallContent functionCall in functionCalls)
                    {
                        Debug.Log($"[MCPChatAI] Вызов тулзы: {functionCall.Name}");

                        var arguments = new Dictionary<string, object?>();

                        if (functionCall.Arguments != null)
                        {
                            foreach (KeyValuePair<string, object> arg in functionCall.Arguments)
                                arguments[arg.Key] = arg.Value;
                        }

                        try
                        {
                            CallToolResult toolResult = await mcpClient.InvokeToolAsync(
                                functionCall.Name,
                                arguments,
                                CancellationToken.None
                            );

                            var resultText = string.Join("\n",
                                toolResult.Content.OfType<TextContentBlock>().Select(t => t.Text));

                            Debug.Log($"[MCPChatAI] Результат тулзы: {resultText}");

                            // Добавляем результат в историю сообщений
                            messages.Add(new ChatMessage(ChatRole.Tool, resultText));
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[MCPChatAI] Ошибка вызова тулзы {functionCall.Name}: {ex.Message}");
                            messages.Add(new ChatMessage(ChatRole.Tool, $"Ошибка: {ex.Message}"));
                        }
                    }

                    // Получаем финальный ответ от AI после выполнения тулз
                    ChatResponse finalResponse = await geminiClient.GetResponseAsync(messages, chatOptions);
                    string finalText = finalResponse.Messages[0].Text ?? "Готово";

                    SendChatMessage($"MCP: {finalText}", channelId, channelType);
                }
                else
                {
                    // Прямой ответ без тулз
                    string responseText = response.Messages[0].Text ?? "Нет ответа";
                    SendChatMessage($"MCP: {responseText}", channelId, channelType);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MCPChatAI] Ошибка обработки: {ex}");
                SendChatMessage($"MCP: Ошибка - {ex.Message}", channelId, channelType);
            }
            finally { isProcessing = false; }
        }

        private void SendChatMessage(string text, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            if (MCPGlobalLocator.ChatMessagesBus == null)
            {
                Debug.LogError("[MCPChatAI] ChatMessagesBus недоступен");
                return;
            }

            // Создаем канал для ответа (тот же канал, откуда пришло сообщение)
            ChatChannel chatChannel;

            if (channelType == ChatChannel.ChatChannelType.NEARBY) { chatChannel = ChatChannel.NEARBY_CHANNEL; }
            else { chatChannel = new ChatChannel(channelType, channelId.Id); }

            var chatMessagesBus = MCPGlobalLocator.ChatMessagesBus as IChatMessagesBus;
            chatMessagesBus.Send(chatChannel, text, "MCP", string.Empty);
            Debug.Log($"[MCPChatAI] Отправлено в чат: {text}");
        }

        private void OnDestroy()
        {
            // Отписываемся от событий
            if (MCPGlobalLocator.ChatMessagesBus != null)
            {
                var chatBus = MCPGlobalLocator.ChatMessagesBus as IChatMessagesBus;

                if (chatBus != null) { chatBus.MessageAdded -= OnChatMessageAdded; }
            }
        }
    }
}
