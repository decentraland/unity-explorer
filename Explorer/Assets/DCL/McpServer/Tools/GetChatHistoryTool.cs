using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Reads the chat history the client holds: the channels and, for one of them, its latest messages
    ///     with the reactions on each. It is what the chat panel renders, so an agent can confirm that a
    ///     message or a reaction from another player arrived, and get the message ids that
    ///     <see cref="ReactToChatMessageTool" /> takes.
    /// </summary>
    public class GetChatHistoryTool : McpTool
    {
        private const string NEARBY = "nearby";
        private const int DEFAULT_LIMIT = 20;
        private const int MAX_LIMIT = 100;

        private readonly IChatHistory chatHistory;
        private readonly List<(int EmojiIndex, int Count)> reactionCounts = new ();

        public override string Name => "get_chat_history";

        public override string Description =>
            "Read the chat messages the client holds, as JSON: 'channels' lists every conversation (Nearby, direct conversations named by "
            + "the other player's wallet, community channels) with its message count; 'messages' holds the latest messages of one channel, "
            + "oldest first, each with its messageId, sender wallet and name, text, timestamp and the reactions on it (emojiIndex, count, "
            + "reactor wallets). Use it to confirm that a message, a direct message or a reaction arrived, and to get a messageId for "
            + "react_to_chat_message.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.String("channel", "The channel to read: 'nearby' (default), the other player's wallet address for a direct conversation, "
                                     + "or a channel id as listed in 'channels'.")
                  .Integer("limit", $"Maximum messages to return (newest win). Default {DEFAULT_LIMIT}, at most {MAX_LIMIT}.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetChatHistoryTool(IChatHistory chatHistory)
        {
            this.chatHistory = chatHistory;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            int limit = Mathf.Clamp(arguments.GetInt("limit", DEFAULT_LIMIT), 1, MAX_LIMIT);
            string requested = arguments.GetString("channel", NEARBY);

            var channels = new JArray();

            foreach (ChatChannel candidate in chatHistory.Channels.Values)
                channels.Add(new JObject
                {
                    ["id"] = candidate.Id.Id,
                    ["type"] = TypeName(candidate.ChannelType),
                    ["messages"] = candidate.Messages.Count,
                });

            var payload = new JObject { ["channels"] = channels };

            if (!TryFindChannel(requested, out ChatChannel? channel))
            {
                // Nearby exists only after its first message: an empty answer, not an error
                if (!string.Equals(requested, NEARBY, StringComparison.OrdinalIgnoreCase))
                    return UniTask.FromResult(McpToolResult.Error($"No channel '{requested}' in the chat history. Channels: {channels.ToString(Newtonsoft.Json.Formatting.None)}"));

                payload["channel"] = ChatChannel.NEARBY_CHANNEL_ID.Id;
                payload["channelType"] = TypeName(ChatChannel.ChatChannelType.NEARBY);
                payload["total"] = 0;
                payload["messages"] = new JArray();

                return UniTask.FromResult(McpToolResult.Json(payload));
            }

            IReadOnlyList<ChatMessage> stored = channel!.Messages;
            int count = Mathf.Min(limit, stored.Count);
            var messages = new JArray();

            // The channel keeps its newest message first; the answer reads oldest first
            for (int i = count - 1; i >= 0; i--)
                messages.Add(Describe(channel, stored[i]));

            payload["channel"] = channel.Id.Id;
            payload["channelType"] = TypeName(channel.ChannelType);
            payload["total"] = stored.Count;
            payload["messages"] = messages;

            return UniTask.FromResult(McpToolResult.Json(payload));
        }

        private bool TryFindChannel(string requested, out ChatChannel? channel)
        {
            if (string.Equals(requested, NEARBY, StringComparison.OrdinalIgnoreCase))
                return chatHistory.Channels.TryGetValue(ChatChannel.NEARBY_CHANNEL_ID, out channel);

            if (chatHistory.Channels.TryGetValue(new ChatChannel.ChannelId(requested), out channel))
                return true;

            // Wallets are stored in lower case; the caller may hold a checksummed address
            foreach (ChatChannel candidate in chatHistory.Channels.Values)
            {
                if (string.Equals(candidate.Id.Id, requested, StringComparison.OrdinalIgnoreCase))
                {
                    channel = candidate;
                    return true;
                }
            }

            channel = null;
            return false;
        }

        private JObject Describe(ChatChannel channel, ChatMessage message)
        {
            var reactions = new JArray();
            ReactionSet? set = string.IsNullOrEmpty(message.MessageId) ? null : channel.GetReactions(message.MessageId);

            if (set != null)
            {
                set.GetAggregateCounts(reactionCounts);

                foreach ((int emojiIndex, int reactorCount) in reactionCounts)
                {
                    var reactors = new JArray();
                    IReadOnlyCollection<string>? wallets = set.GetReactors(emojiIndex);

                    if (wallets != null)
                        foreach (string wallet in wallets)
                            reactors.Add(wallet);

                    reactions.Add(new JObject
                    {
                        ["emojiIndex"] = emojiIndex,
                        ["count"] = reactorCount,
                        ["reactors"] = reactors,
                    });
                }
            }

            return new JObject
            {
                ["messageId"] = message.MessageId,
                ["sender"] = message.SenderWalletAddress,
                ["senderName"] = message.SenderValidatedName,
                ["message"] = message.Message,
                ["timestamp"] = message.SentTimestampRaw,
                ["sentAtUtc"] = message.SentTimestamp?.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                ["sentByOwnUser"] = message.IsSentByOwnUser,
                ["system"] = message.IsSystemMessage,
                ["reactions"] = reactions,
            };
        }

        private static string TypeName(ChatChannel.ChatChannelType type)
        {
            switch (type)
            {
                case ChatChannel.ChatChannelType.NEARBY: return "nearby";
                case ChatChannel.ChatChannelType.COMMUNITY: return "community";
                case ChatChannel.ChatChannelType.USER: return "user";
                default: return "undefined";
            }
        }
    }
}
