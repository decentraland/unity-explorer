using Cysharp.Threading.Tasks;
using DCL.Chat.ChatServices;
using DCL.Chat.History;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DCL.Chat.Commands
{
    /// <summary>
    /// Floods per-message chat reactions onto a message in the current channel to stress-test the
    /// reaction-flood hardening: the <see cref="ReactionSet"/> distinct-emoji and per-emoji reactor caps,
    /// the message feed presenter's per-frame coalescing, and the render-boundary clamp.
    /// The count is intentionally unclamped so QA can push arbitrarily large floods.
    /// </summary>
    public class FloodReactionsChatCommand : IChatCommand
    {
        // Just above ReactionSet.MAX_DISTINCT_EMOJIS (20) so the distinct-emoji cap is exercised while
        // staying inside a typical reaction atlas so the pills still render.
        private const int EMOJI_CYCLE = 24;
        private const int TOGGLE_EMOJI_INDEX = 0;
        private const int REACTIONS_PER_FRAME = 500;
        private const string TOGGLE_WALLET = "0xflood-toggle";

        private readonly CurrentChannelService currentChannelService;

        public string Command => "floodreactions";
        public string Description => "<b>/floodreactions <i>count [messageId]</i></b>\n  Flood 'count' chat reactions onto the latest (or given) message in the current channel";
        public bool DebugOnly => true;

        public FloodReactionsChatCommand(CurrentChannelService currentChannelService)
        {
            this.currentChannelService = currentChannelService;
        }

        public bool ValidateParameters(string[] parameters) =>
            parameters.Length is 1 or 2
            && int.TryParse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
            && count > 0;

        public async UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            int count = int.Parse(parameters[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
            ChatChannel channel = currentChannelService.CurrentChannel;

            string messageId;

            if (parameters.Length == 2)
            {
                messageId = parameters[1];

                if (!ChannelContainsMessage(channel, messageId))
                    return $"🔴 Message '{messageId}' was not found in the current channel.";
            }
            else
            {
                if (channel.Messages.Count == 0)
                    return "🔴 The current channel has no messages to react on. Send a message first.";

                messageId = channel.Messages[0].MessageId;
            }

            int accepted = 0;
            int rejected = 0;
            int processed = 0;

            for (int i = 0; i < count; i++)
            {
                if (channel.AddReaction(messageId, i % EMOJI_CYCLE, $"0xflood{i}"))
                    accepted++;
                else
                    rejected++;

                // Add then immediately remove one wallet on a single emoji so the removal path fires too.
                channel.AddReaction(messageId, TOGGLE_EMOJI_INDEX, TOGGLE_WALLET);
                channel.RemoveReaction(messageId, TOGGLE_EMOJI_INDEX, TOGGLE_WALLET);

                processed = i + 1;

                // Spread the flood across frames so it mimics a sustained network flood and the per-frame
                // reaction coalescing (the hardening being verified) is exercised over many frames.
                if (processed % REACTIONS_PER_FRAME == 0)
                {
                    bool cancelled = await UniTask.NextFrame(ct).SuppressCancellationThrow();

                    if (cancelled)
                        break;
                }
            }

            string summary = $"Flooded {processed}/{count} reactions on message '{messageId}' (accepted {accepted}, rejected {rejected} by caps).";
            ReportHub.Log(ReportCategory.CHAT_MESSAGES, $"[FloodReactions] {summary}");
            return summary;
        }

        private static bool ChannelContainsMessage(ChatChannel channel, string messageId)
        {
            IReadOnlyList<ChatMessage> messages = channel.Messages;

            for (int i = 0; i < messages.Count; i++)
                if (messages[i].MessageId == messageId)
                    return true;

            return false;
        }
    }
}
