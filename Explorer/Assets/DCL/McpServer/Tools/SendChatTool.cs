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
    ///     Sends a message to the Nearby chat channel, or as a direct message to one player. Nearby messages
    ///     starting with '/' run through the chat command pipeline, so agents can drive commands the same way
    ///     a user typing in chat would.
    /// </summary>
    public class SendChatTool : McpTool
    {
        private const int MAX_MESSAGE_LENGTH = 500;

        // "0x" followed by 40 hexadecimal digits
        private const int WALLET_ADDRESS_LENGTH = 42;

        private readonly IChatMessagesBus chatMessagesBus;

        public override string Name => "send_chat";

        public override string Description =>
            "Send a message to the Nearby chat channel, or with 'to' as a direct message to one player. Nearby messages starting with '/' "
            + "run chat commands (e.g. /goto x,y, /reload, /help); command output appears in chat and scene logs. A direct message goes "
            + "straight to the chat bus: the chat panel's own rules (friends-only privacy, blocked users) are not applied. "
            + "Read what arrived with get_chat_history.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("message", "The chat message or /command to send.", isRequired: true)
                  .String("to", "Wallet address (0x and 40 hex digits) of the player to send a direct message to. Omit to send to Nearby.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public SendChatTool(IChatMessagesBus chatMessagesBus)
        {
            this.chatMessagesBus = chatMessagesBus;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string message = arguments.GetString("message", string.Empty);

            if (string.IsNullOrWhiteSpace(message))
                return UniTask.FromResult(McpToolResult.Error("message is required."));

            if (message.Length > MAX_MESSAGE_LENGTH)
                return UniTask.FromResult(McpToolResult.Error($"message exceeds the {MAX_MESSAGE_LENGTH} character limit."));

            string? to = arguments.GetStringOrNull("to");

            if (string.IsNullOrEmpty(to))
            {
                chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, message, ChatMessageOrigin.Chat);
                return UniTask.FromResult(McpToolResult.Text($"Sent to Nearby: {message}"));
            }

            if (!IsWalletAddress(to))
                return UniTask.FromResult(McpToolResult.Error("to must be a wallet address: 0x followed by 40 hexadecimal digits."));

            // The bus runs every message that starts with '/' as a command, whatever its channel
            if (message[0] == '/')
                return UniTask.FromResult(McpToolResult.Error("A direct message cannot start with '/': chat commands run only without 'to'."));

            // Channel ids of direct conversations are the other player's wallet in lower case, as incoming messages name them
            var channel = new ChatChannel(ChatChannel.ChatChannelType.USER, to.ToLowerInvariant());
            chatMessagesBus.SendWithUtcNowTimestamp(channel, message, ChatMessageOrigin.Chat);

            return UniTask.FromResult(McpToolResult.Text($"Sent to {channel.Id.Id}: {message}"));
        }

        private static bool IsWalletAddress(string value)
        {
            if (value.Length != WALLET_ADDRESS_LENGTH || value[0] != '0' || (value[1] != 'x' && value[1] != 'X'))
                return false;

            for (var i = 2; i < value.Length; i++)
            {
                char c = value[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

                if (!isHex)
                    return false;
            }

            return true;
        }
    }
}
