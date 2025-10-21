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
            if (!message.IsSentByOwnUser || message.IsSystemMessage || message.IsMention)
                return;

            Debug.Log("MCP: Chat message received.");

            if (isProcessing)
            {
                Debug.Log("MCP: Обрабатываю предыдущий запрос, подождите...");
                return;
            }

            Debug.Log($"[MCPChatAI] Получен запрос: {message.Message}");

            ProcessAIQueryAsync(message.Message, channelId, channelType).Forget();
        }

        private async UniTask ProcessAIQueryAsync(string query, ChatChannel.ChannelId channelId, ChatChannel.ChatChannelType channelType)
        {
            isProcessing = true;

            try
            {
                if (geminiClient == null || availableTools == null || !availableTools.Any())
                {
                    Debug.LogWarning("MCP: AI не инициализирован или нет доступных инструментов.");
                    return;
                }

                var chatOptions = new ChatOptions { Tools = availableTools.ToArray<AITool>() };

                var messages = new List<ChatMessage>
                {
                    new (ChatRole.User, query),
                };

                const int maxTurns = 5; // Безопасный лимит для предотвращения бесконечных циклов

                for (var turn = 0; turn < maxTurns; turn++)
                {
                    Debug.Log($"[MCPChatAI] Отправка запроса к AI. Итерация: {turn + 1}/{maxTurns}. Сообщений в истории: {messages.Count}.");

                    ChatResponse response = await geminiClient.GetResponseAsync(messages, chatOptions);

                    if (response.Messages == null || !response.Messages.Any())
                    {
                        Debug.LogError("[MCPChatAI] AI вернул пустой или некорректный ответ.");
                        return;
                    }

                    // Обычно в не-стриминговом режиме от ассистента приходит одно сообщение
                    ChatMessage assistantMessage = response.Messages[0];
                    messages.Add(assistantMessage);

                    // Сначала извлекаем текстовую часть, так как она может идти вместе с вызовом инструментов
                    string responseText = string.Join("\n", assistantMessage.Contents.OfType<TextContent>().Select(c => c.Text)).Trim();

                    IEnumerable<FunctionCallContent> functionCalls = assistantMessage.Contents.OfType<FunctionCallContent>();

                    if (!functionCalls.Any())
                    {
                        Debug.Log($"[MCPChatAI] Получен финальный текстовый ответ: '{responseText}'");

                        if (string.IsNullOrWhiteSpace(responseText))
                        {
                            responseText = assistantMessage.Text ?? "Готово."; // Запасной вариант
                            Debug.Log($"[MCPChatAI] Ответ извлечен из свойства .Text: '{responseText}'");
                        }

                        SendChatMessage($"MCP: {responseText}", channelId, channelType);
                        return; // Конец обработки
                    }

                    // Если был текст вместе с вызовом инструментов, покажем его пользователю
                    if (!string.IsNullOrWhiteSpace(responseText))
                        SendChatMessage($"MCP: {responseText}", channelId, channelType);

                    Debug.Log($"[MCPChatAI] AI запросил вызов {functionCalls.Count()} инструментов.");

                    // Выполняем все запрошенные вызовы
                    foreach (FunctionCallContent functionCall in functionCalls)
                    {
                        Debug.Log($"[MCPChatAI] Вызов: {functionCall.Name}");

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

                            Debug.Log($"[MCPChatAI] Результат '{functionCall.Name}': {resultText.Substring(0, Math.Min(resultText.Length, 120))}...");

                            messages.Add(new ChatMessage(ChatRole.Tool, resultText));
                        }
                        catch (Exception ex)
                        {
                            var errorMessage = $"Ошибка вызова инструмента {functionCall.Name}: {ex.Message}";
                            Debug.LogError($"[MCPChatAI] {errorMessage}");
                            messages.Add(new ChatMessage(ChatRole.Tool, errorMessage));
                        }
                    }
                }

                // Если цикл завершился по лимиту итераций
                Debug.LogWarning("MCP: Превышено максимальное количество вызовов инструментов.");
            }
            catch (Exception ex)
            {
                // Проверяем, не является ли это ошибкой о превышении лимитов
                if (ex.Message.Contains("429") && ex.Message.ToLower().Contains("rate limit")) { Debug.LogWarning("MCP: Превышен лимит запросов к Gemini API (15 в минуту). Пожалуйста, подождите."); }
                else { Debug.LogError($"MCP: Ошибка обработки запроса: {ex.Message}"); }
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
