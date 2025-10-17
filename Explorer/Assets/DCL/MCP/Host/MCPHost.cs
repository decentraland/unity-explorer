using Cysharp.Threading.Tasks;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Mscc.GenerativeAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using UnityEngine;

public class MCPHost
{
    private readonly McpServer server;
    private readonly GenerativeModel model;
    private readonly McpClient client;

    private IList<McpClientTool> tools;

    public MCPHost(McpClient client, McpServer server, GenerativeModel model)
    {
        this.client = client;
        this.server = server;
        this.model = model;
    }

    public async UniTask AskGeminiToCallToolAsync(CancellationToken ct)
    {
        try
        {
            tools ??= await client.ListToolsAsync(cancellationToken: ct);

            var toolsList = string.Join(", ", tools.Select(t => t.Name));
            var system = "Ты планировщик MCP. Верни строго валидный JSON без каких-либо комментариев и пояснений.";

            var instruction =
                $"Доступные MCP tools: [{toolsList}]. Сформируй JSON команду формата {{\"tool\":\"<NAME>\",\"args\":{{\"arg\":\"<value>\"}}}}. Для инструмента Echo используй параметр с именем 'arg'. Верни только JSON.";

            GenerateContentResponse response = await model.GenerateContent(system + "\n" + instruction);
            string text = response.Text?.Trim();

            if (!string.IsNullOrEmpty(text))
            {
                string ExtractJson(string s)
                {
                    int start = s.IndexOf('{');
                    int end = s.LastIndexOf('}');

                    if (start >= 0 && end > start)
                        return s.Substring(start, end - start + 1);

                    return s;
                }

                if (text.StartsWith("```"))
                {
                    int firstNewLine = text.IndexOf('\n');

                    if (firstNewLine > 0)
                        text = text.Substring(firstNewLine + 1);

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

            using var doc = JsonDocument.Parse(text);
            JsonElement root = doc.RootElement;
            string toolName = root.GetProperty("tool").GetString();
            var args = new Dictionary<string, object?>();

            if (root.TryGetProperty("args", out JsonElement argsEl))
            {
                foreach (JsonProperty p in argsEl.EnumerateObject())
                {
                    args[p.Name] = p.Value.ValueKind switch
                                   {
                                       JsonValueKind.String => p.Value.GetString(),
                                       JsonValueKind.Number => p.Value.TryGetInt64(out long li) ? li : p.Value.GetDouble(),
                                       JsonValueKind.True => true,
                                       JsonValueKind.False => false,
                                       _ => p.Value.ToString(),
                                   };
                }
            }

            Debug.Log($"[MCP] Gemini tool: {toolName}; args: {string.Join(", ", args.Select(kv => kv.Key + ":" + kv.Value))}");

            CallToolResult result = await client.CallToolAsync(toolName, new Dictionary<string, object?>(args), cancellationToken: ct);

            if (result?.Content != null && result.Content.Count > 0)
            {
                foreach (ContentBlock c in result.Content)
                {
                    switch (c)
                    {
                        case TextContentBlock t:
                            Debug.Log($"[MCP] Tool result (text): {t.Text}");
                            break;
                        default:
                            Debug.Log($"[MCP] Tool result ({c.Type})");
                            break;
                    }
                }
            }
            else { Debug.Log("[MCP] Tool returned no content"); }
        }
        catch (Exception ex) { Debug.LogError($"[MCP] AskGeminiToCallTool failed: {ex.Message}"); }
    }
}
