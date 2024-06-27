using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using Segment.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatAnalytics
    {
        private static readonly Regex EMOJI_PATTERN = new (@"\\U[0-9A-Fa-f]{8}");

        private readonly AnalyticsController analytics;
        private readonly ChatController chatController;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IChatCommand teleportToCommand;

        private bool isInitChatBubble = true;

        public ChatAnalytics(AnalyticsController analytics, ChatController chatController, IChatMessagesBus chatMessagesBus, IChatCommand teleportToCommand)
        {
            this.analytics = analytics;
            this.chatController = chatController;
            this.chatMessagesBus = chatMessagesBus;
            this.teleportToCommand = teleportToCommand;

            chatController.ChatBubbleVisibilityChanged += OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent += OnMessageSent;
            teleportToCommand.Executed += OnTeleportedViaGoTo;
        }

        ~ChatAnalytics()
        {
            chatController.ChatBubbleVisibilityChanged -= OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent -= OnMessageSent;
            teleportToCommand.Executed -= OnTeleportedViaGoTo;
        }

        private void OnMessageSent(string message)
        {
            analytics.Track("chat_message_sent", new Dictionary<string, JsonElement>
            {
                { "message", message },
                { "emojis_amount", EMOJI_PATTERN.Matches(message).Count },
            });
        }

        private void OnTeleportedViaGoTo()
        {
            analytics.Track("goto_teleport");
        }

        private void OnChatBubbleVisibilityChanged(bool isVisible)
        {
            if (isInitChatBubble)
            {
                isInitChatBubble = false;
                return;
            }

            analytics.Track("chat_bubble_switched", new Dictionary<string, JsonElement>
            {
                { "is_visible", isVisible },
            });
        }
    }
}
