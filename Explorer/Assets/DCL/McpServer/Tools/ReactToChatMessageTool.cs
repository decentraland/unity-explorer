using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.History;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Toggles the local player's reaction on a chat message through <see cref="ChatMessageReactionService" />,
    ///     the path a click on a reaction in the chat panel takes: the history changes and the reaction is
    ///     sent to the message's channel.
    /// </summary>
    public class ReactToChatMessageTool : McpTool
    {
        private readonly IChatHistory chatHistory;
        private readonly MessageReactionServiceSlot reactionService;

        public override string Name => "react_to_chat_message";

        public override string Description =>
            "Toggle the local player's reaction on a chat message, as a click on a reaction in the chat panel does: the first call with an "
            + "emoji adds the reaction and sends it to the message's channel (Nearby, a direct conversation or a community), a second call "
            + "with the same emoji removes it. Answers with the resulting state ('added' or 'removed') and the reactor count. Get messageId "
            + "and the emojiIndex values other players used from get_chat_history.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("messageId", "The id of the message to react to, as get_chat_history returns it.", isRequired: true)
                  .Integer("emojiIndex", "The emoji as its index in the reactions emoji atlas (0 or greater): the number every client puts on the wire.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ReactToChatMessageTool(IChatHistory chatHistory, MessageReactionServiceSlot reactionService)
        {
            this.chatHistory = chatHistory;
            this.reactionService = reactionService;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            string messageId = arguments.GetString("messageId", string.Empty);

            if (string.IsNullOrEmpty(messageId))
                return UniTask.FromResult(McpToolResult.Error("messageId is required."));

            if (!arguments.TryGetInt("emojiIndex", out int emojiIndex))
                return UniTask.FromResult(McpToolResult.Error($"emojiIndex is required{arguments.NonNumericHint("emojiIndex")}."));

            // A negative value on the wire means "remove": the toggle decides that, not the caller
            if (emojiIndex < 0)
                return UniTask.FromResult(McpToolResult.Error("emojiIndex must be 0 or greater."));

            ChatMessageReactionService? service = reactionService.Service;

            if (service == null)
                return UniTask.FromResult(McpToolResult.Error("Chat reactions are not ready yet: wait until the chat has finished loading."));

            if (!TryFindChannel(messageId, out ChatChannel? channel))
                return UniTask.FromResult(McpToolResult.Error($"No message with id '{messageId}' in the chat history. Read the ids with get_chat_history."));

            int before = ReactorCount(channel!, messageId, emojiIndex);
            service.ToggleReaction(messageId, emojiIndex);
            int after = ReactorCount(channel!, messageId, emojiIndex);

            if (after == before)
                return UniTask.FromResult(McpToolResult.Error("The reaction was not applied: the player has no identity yet, or the message already holds "
                                                              + "the most distinct emojis it can."));

            return UniTask.FromResult(McpToolResult.Json(new JObject
            {
                ["messageId"] = messageId,
                ["channel"] = channel!.Id.Id,
                ["emojiIndex"] = emojiIndex,
                ["state"] = after > before ? "added" : "removed",
                ["reactors"] = after,
            }));
        }

        private bool TryFindChannel(string messageId, out ChatChannel? channel)
        {
            foreach (ChatChannel candidate in chatHistory.Channels.Values)
            {
                IReadOnlyList<ChatMessage> messages = candidate.Messages;

                for (var i = 0; i < messages.Count; i++)
                {
                    if (messages[i].MessageId == messageId)
                    {
                        channel = candidate;
                        return true;
                    }
                }
            }

            channel = null;
            return false;
        }

        private static int ReactorCount(ChatChannel channel, string messageId, int emojiIndex) =>
            channel.GetReactions(messageId)?.GetReactors(emojiIndex)?.Count ?? 0;
    }
}
