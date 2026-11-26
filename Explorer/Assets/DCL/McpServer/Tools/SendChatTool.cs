using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat.MessageBus;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.RealmNavigation;
using DCL.Web3;
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

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ILoadingStatus loadingStatus;

        public override string Name => "send_chat";

        public override string Description =>
            "Send a message to the Nearby chat channel, or with 'to' as a direct message to one player. Nearby messages starting with '/' "
            + "run chat commands (e.g. /goto x,y, /reload, /help); command output appears in chat and scene logs. A direct message goes "
            + "straight to the chat bus: the chat panel's own rules (friends-only privacy, blocked users) are not applied. "
            + "Nothing can be sent while the client is still loading. Read what arrived with get_chat_history.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("message", "The chat message or /command to send.", isRequired: true)
                  .String("to", "Wallet address (0x and 40 hex digits) of the player to send a direct message to. Omit to send to Nearby.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public SendChatTool(IChatMessagesBus chatMessagesBus, ILoadingStatus loadingStatus)
        {
            this.chatMessagesBus = chatMessagesBus;
            this.loadingStatus = loadingStatus;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string message = arguments.GetString("message", string.Empty);

            if (string.IsNullOrWhiteSpace(message))
                return UniTask.FromResult(McpToolResult.Error("message is required."));

            if (message.Length > MAX_MESSAGE_LENGTH)
                return UniTask.FromResult(McpToolResult.Error($"message exceeds the {MAX_MESSAGE_LENGTH} character limit."));

            // The bus drops every message, commands included, until loading completes
            if (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
                return UniTask.FromResult(McpToolResult.Error("The chat drops messages while the client is loading: retry once loading completes."));

            // Only an absent or null `to` means Nearby: any other value must name a wallet, or a message meant for one player would be broadcast
            JToken? toArgument = arguments["to"];

            if (toArgument == null || toArgument.Type == JTokenType.Null)
            {
                chatMessagesBus.SendWithUtcNowTimestamp(ChatChannel.NEARBY_CHANNEL, message, ChatMessageOrigin.Chat);
                return UniTask.FromResult(McpToolResult.Text($"Sent to Nearby: {message}"));
            }

            Web3Address? recipient = Web3Address.FromUntrusted(arguments.GetStringOrNull("to"));

            if (recipient == null)
                return UniTask.FromResult(McpToolResult.Error("to must be a wallet address: 0x followed by 40 hexadecimal digits."));

            // The bus runs every message that starts with '/' as a command, whatever its channel
            if (message[0] == '/')
                return UniTask.FromResult(McpToolResult.Error("A direct message cannot start with '/': chat commands run only without 'to'."));

            // Channel ids of direct conversations are the other player's wallet in lower case, as incoming messages name them
            var channel = new ChatChannel(ChatChannel.ChatChannelType.USER, recipient.Value.ToString());
            chatMessagesBus.SendWithUtcNowTimestamp(channel, message, ChatMessageOrigin.Chat);

            return UniTask.FromResult(McpToolResult.Text($"Sent to {channel.Id.Id}: {message}"));
        }
    }
}
