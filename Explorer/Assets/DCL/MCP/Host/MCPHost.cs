using Cysharp.Threading.Tasks;
using DCL.MCP.Host;
using ModelContextProtocol.Protocol;
using Mscc.GenerativeAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using UnityEngine;

public class MCPHost : MonoBehaviour
{
    public string modelName;

    [SerializeField] private string geminiApiKey;
    [SerializeField] private string defaultUnityWsUrl = "ws://localhost:7777";
    [SerializeField] private MCPClient mcpClient;

    [ContextMenu("TEST AI")]
    public void TestGoogleAPI()
    {
        CallG().Forget();
        return;

        async UniTask CallG()
        {
            var googleAI = new GoogleAI(geminiApiKey);

            GenerativeModel model = googleAI.GenerativeModel();
            string prompt = "Объясни, что такое API, простыми словами для новичка.";
            Debug.Log($"Ваш запрос: {prompt}\n");

            try
            {
                GenerateContentResponse response = await model.GenerateContent(prompt);
                Debug.Log($"Ответ Gemini: {response.Text}");
            }
            catch (Exception ex) { Debug.Log($"Произошла ошибка: {ex.Message}"); }
        }
    }

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
            // 1) Попросим Gemini выдать ДОЛЖНО-ТОЛЬКО JSON команды для MCP tool
            var googleAI = new GoogleAI(string.IsNullOrWhiteSpace(geminiApiKey) ? Environment.GetEnvironmentVariable("GEMINI_API_KEY") : geminiApiKey);
            GenerativeModel model = string.IsNullOrWhiteSpace(modelName) ? googleAI.GenerativeModel() : googleAI.GenerativeModel(modelName);

            string system = "Ты инструктор. Верни строго валидный JSON без каких-либо комментариев и пояснений.";
            string instruction = $"Сформируй JSON команду для подключения к Unity по WebSocket. Используй MCP tool connect_to_unity_ws и аргумент url='{defaultUnityWsUrl}'. Формат: {{\"tool\":\"connect_to_unity_ws\",\"args\":{{\"url\":\"{defaultUnityWsUrl}\"}}}}. Верни только JSON.";

            GenerateContentResponse response = await model.GenerateContent(system + "\n" + instruction);
            string text = response.Text?.Trim();

            // Удаляем markdown-кодовые блоки и оставляем только JSON
            if (!string.IsNullOrEmpty(text))
            {
                string ExtractJson(string s)
                {
                    // Варианты: ```json { ... } ``` или ``` { ... } ``` или просто { ... }
                    int start = s.IndexOf('{');
                    int end = s.LastIndexOf('}');

                    if (start >= 0 && end > start)
                        return s.Substring(start, end - start + 1);

                    return s;
                }

                // Срезаем возможные тройные бэктики
                if (text.StartsWith("```"))
                {
                    // убираем первые три бэктика и возможную метку языка
                    int firstNewLine = text.IndexOf('\n');

                    if (firstNewLine > 0)
                        text = text.Substring(firstNewLine + 1);

                    // убираем закрывающие бэктики
                    int closeIdx = text.LastIndexOf("```", StringComparison.Ordinal);

                    if (closeIdx >= 0)
                        text = text.Substring(0, closeIdx);

                    text = text.Trim();
                }

                text = ExtractJson(text).Trim();
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogError("[MCP] Gemini returned empty response");
                return;
            }

            // 2) Парсим JSON с полями tool/args
            using var doc = JsonDocument.Parse(text);
            JsonElement root = doc.RootElement;
            string tool = root.GetProperty("tool").GetString();
            var args = new Dictionary<string, object?>();

            if (root.TryGetProperty("args", out JsonElement argsEl))
                foreach (JsonProperty p in argsEl.EnumerateObject())
                    args[p.Name] = p.Value.ValueKind switch
                                   {
                                       JsonValueKind.String => p.Value.GetString(),
                                       JsonValueKind.Number => p.Value.TryGetInt64(out long li) ? li : p.Value.GetDouble(),
                                       JsonValueKind.True => true,
                                       JsonValueKind.False => false,
                                       _ => p.Value.ToString(),
                                   };

            Debug.Log($"[MCP] Gemini tool: {tool}; args: {string.Join(", ", args.Select(kv => kv.Key + ":" + kv.Value))}");

            // 3) Вызов MCP tool через клиент
            await mcpClient.EnsureConnectedAsync(CancellationToken.None);
            CallToolResult result = await mcpClient.InvokeToolAsync(tool, args, CancellationToken.None);

            // 4) Логируем ответ
            if (result?.Content != null && result.Content.Count > 0)
                foreach (ContentBlock c in result.Content)
                    switch (c)
                    {
                        case TextContentBlock t:
                            Debug.Log($"[MCP] Tool result (text): {t.Text}");
                            break;
                        default:
                            Debug.Log($"[MCP] Tool result ({c.Type})");
                            break;
                    }
            else
                Debug.Log("[MCP] Tool returned no content");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MCP] AskGeminiToConnect failed: {ex.Message}");
        }
    }
}
