using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Sends a message to the Nearby chat channel. Messages starting with '/' run through the chat
    ///     command pipeline, so agents can drive commands the same way a user typing in chat would.
    /// </summary>
    public class SendChatTool : McpTool
    {
        private const int MAX_MESSAGE_LENGTH = 500;

        private readonly IChatMessagesBus chatMessagesBus;

        public override string Name => "send_chat";

        public override string Description =>
            "Send a message to the Nearby chat channel. Messages starting with '/' run chat commands "
            + "(e.g. /goto x,y, /reload, /help); command output appears in chat and scene logs.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("message", "The chat message or /command to send.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public SendChatTool(IChatMessagesBus chatMessagesBus)
        {
            this.chatMessagesBus = chatMessagesBus;
        }

        protected override UniTask<McpToolResult> ExecuteCoreAsync(JObject arguments, CancellationToken ct)
        {
            string message = arguments.GetString("message", string.Empty);

            if (string.IsNullOrWhiteSpace(message))
                return UniTask.FromResult(McpToolResult.Error("message is required."));

            if (message.Length > MAX_MESSAGE_LENGTH)
                return UniTask.FromResult(McpToolResult.Error($"message exceeds the {MAX_MESSAGE_LENGTH} character limit."));

            chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, message, ChatMessageOrigin.CHAT);

            return UniTask.FromResult(McpToolResult.Text($"Sent to Nearby: {message}"));
        }
    }
}
