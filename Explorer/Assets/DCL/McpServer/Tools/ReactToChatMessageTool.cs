using Cysharp.Threading.Tasks;
using DCL.Chat.ChatReactions.Networking;
using DCL.Chat.History;
using DCL.FeatureFlags;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private readonly IReadonlyReactiveProperty<ChatMessageReactionService?> reactionService;

        public override string Name => "react_to_chat_message";

        public override string Description =>
            "Toggle the local player's reaction on a chat message, as a click on a reaction in the chat panel does: the first call with an "
            + "emoji adds the reaction and sends it to the message's channel (Nearby, a direct conversation or a community), a second call "
            + "with the same emoji removes it. Answers with the resulting state ('added' or 'removed') and the reactor count. Get messageId "
            + "and the emojiIndex values other players used from get_chat_history.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("messageId", "The id of the message to react to, as get_chat_history returns it.", isRequired: true)
                  .Integer("emojiIndex", "The emoji as its index in the reactions emoji atlas (0 up to the atlas size): the number every client puts on the wire.", isRequired: true);

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: false);

        public ReactToChatMessageTool(IChatHistory chatHistory, IReadonlyReactiveProperty<ChatMessageReactionService?> reactionService)
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

            ChatMessageReactionService? service = reactionService.Value;

            if (service == null)
                return UniTask.FromResult(McpToolResult.Error("Chat reactions are not ready yet: wait until the chat has finished loading."));

            // With the flag off the service has no network bus: the reaction would change the local history and reach nobody
            if (!FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CHAT_REACTIONS_ENABLED))
                return UniTask.FromResult(McpToolResult.Error("Chat reactions are disabled by feature flag: nothing would reach the channel."));

            // A negative value on the wire means "remove", which the toggle decides; receivers drop an index outside the atlas
            if (emojiIndex < 0 || emojiIndex >= service.EmojiCount)
                return UniTask.FromResult(McpToolResult.Error($"emojiIndex must be between 0 and {service.EmojiCount - 1}, the reactions emoji atlas."));

            if (!TryFindChannel(messageId, out ChatChannel? channel))
                return UniTask.FromResult(McpToolResult.Error($"No message with id '{messageId}' in the chat history. Read the ids with get_chat_history."));

            // FillChannel raises no MessageAdded, so restored messages stay unknown to the service until registered
            service.RegisterChannelMessages(channel);

            int before = ReactorCount(channel, messageId, emojiIndex);
            service.ToggleReaction(messageId, emojiIndex);
            int after = ReactorCount(channel, messageId, emojiIndex);

            if (after == before)
                return UniTask.FromResult(McpToolResult.Error("The reaction was not applied: the player has no identity yet, or the message reached its "
                                                              + "distinct-emoji or per-emoji reactor limit."));

            return UniTask.FromResult(McpToolResult.Json(new JObject
            {
                ["messageId"] = messageId,
                ["channel"] = channel.Id.Id,
                ["emojiIndex"] = emojiIndex,
                ["state"] = after > before ? "added" : "removed",
                ["reactors"] = after,
            }));
        }

        private bool TryFindChannel(string messageId, [NotNullWhen(true)] out ChatChannel? channel)
        {
            foreach (ChatChannel candidate in chatHistory.Channels.Values)
            {
                IReadOnlyList<ChatMessage> messages = candidate.Messages;

                for (var i = 0; i < messages.Count; i++)
                {
                    // A system message has an id only this client knows, and the chat panel offers no reactions on it
                    if (messages[i].MessageId == messageId && !messages[i].IsSystemMessage)
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
