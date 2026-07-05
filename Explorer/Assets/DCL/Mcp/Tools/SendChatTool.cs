using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.Mcp.Protocol;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.Mcp.Tools
{
    public class SendChatTool : IMcpTool
    {
        private const int MAX_MESSAGE_LENGTH = 500;

        private readonly IChatMessagesBus chatMessagesBus;

        public string Name => "send_chat";

        public string Description =>
            "Send a message to the Nearby chat channel. Messages starting with '/' run chat commands "
            + "(e.g. /goto x,y, /reload, /help); command output appears in chat and scene logs.";

        public string InputSchemaJson =>
            @"{
                ""type"": ""object"",
                ""properties"": {
                    ""message"": { ""type"": ""string"", ""description"": ""The chat message or /command to send."" }
                },
                ""required"": [""message""]
            }";

        internal SendChatTool(IChatMessagesBus chatMessagesBus)
        {
            this.chatMessagesBus = chatMessagesBus;
        }

        public async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string message = arguments.GetString("message", string.Empty);

            if (string.IsNullOrWhiteSpace(message))
                return McpToolResult.Error("message is required.");

            if (message.Length > MAX_MESSAGE_LENGTH)
                return McpToolResult.Error($"message exceeds the {MAX_MESSAGE_LENGTH} character limit.");

            await UniTask.SwitchToMainThread(ct);

            chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, message, ChatMessageOrigin.CHAT);

            return McpToolResult.Text($"Sent to Nearby: {message}");
        }
    }
}
