using Cysharp.Threading.Tasks;
using DCL.MCP.Host;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Microsoft.Extensions.AI;
using Mscc.GenerativeAI.Microsoft;
using ModelContextProtocol.Client;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

public class MCPHost : MonoBehaviour
{
    public string modelName;

    [SerializeField] private string geminiApiKey;
    [SerializeField] private string defaultUnityWsUrl = "ws://localhost:7777";
    [SerializeField] private MCPClient mcpClient;

    // [ContextMenu("TEST AI")]
    // public void TestGoogleAPI()
    // {
    //     CallG().Forget();
    //     return;
    //
    //     async UniTask CallG()
    //     {
    //         var googleAI = new GoogleAI(geminiApiKey);
    //
    //         GenerativeModel model = googleAI.GenerativeModel();
    //         string prompt = "Объясни, что такое API, простыми словами для новичка.";
    //         Debug.Log($"Ваш запрос: {prompt}\n");
    //
    //         try
    //         {
    //             GenerateContentResponse response = await model.GenerateContent(prompt);
    //             Debug.Log($"Ответ Gemini: {response.Text}");
    //         }
    //         catch (Exception ex) { Debug.Log($"Произошла ошибка: {ex.Message}"); }
    //     }
    // }

    [ContextMenu("Gemini → MCP connect")]
    public void AskGeminiToConnect() =>
        AskGeminiToConnectAsync().Forget();

    private async UniTask AskGeminiToConnectAsync()
    {
        if (mcpClient == null)
        {
            Debug.LogError("[MCP] MCPClient reference is not assigned in MCPHost");
            return;
        }

        try
        {
            IList<McpClientTool> tools = await mcpClient.GetToolsAsync();

            var chatOptions = new ChatOptions
            {
                Tools = tools.ToArray<AITool>(),
            };

            IChatClient geminiClient = new GeminiChatClient(geminiApiKey, modelName);

            // История сообщений
            var messages = new List<ChatMessage>
            {
                new (ChatRole.User, "подключись к DCL Explorer"),
            };

            // Первый запрос
            ChatResponse response = await geminiClient.GetResponseAsync(messages, chatOptions);

            // Проверяем function calls
            IEnumerable<FunctionCallContent> functionCalls = response.Messages[0].Contents.OfType<FunctionCallContent>();

            if (functionCalls.Any())
            {
                // Добавляем ответ модели в историю
                messages.Add(response.Messages[0]);

                foreach (FunctionCallContent functionCall in functionCalls)
                {
                    Debug.Log($"[MCP] Calling tool: {functionCall.Name}");

                    // Вызываем MCP tool
                    var arguments = new Dictionary<string, object?>();

                    if (functionCall.Arguments != null)
                    {
                        foreach (KeyValuePair<string, object> arg in functionCall.Arguments)
                            arguments[arg.Key] = arg.Value;
                    }

                    CallToolResult toolResult = await mcpClient.InvokeToolAsync(
                        functionCall.Name,
                        arguments,
                        CancellationToken.None
                    );

                    // Формируем результат для модели
                    var resultText = string.Join("\n",
                        toolResult.Content.OfType<TextContentBlock>().Select(t => t.Text));

                    Debug.Log($"[MCP] Tool returned: {resultText}");
                }

                // Отправляем результаты обратно модели для финального ответа
                ChatResponse finalResponse = await geminiClient.GetResponseAsync(messages, chatOptions);
                Debug.Log($"[MCP] Final response: {finalResponse.Messages[0].Text}");
            }
            else { Debug.Log($"[MCP] Direct response: {response.Messages[0].Text}"); }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MCP] AskGeminiToConnect failed: {ex.Message}");
        }
    }
}
