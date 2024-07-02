using DCL.Chat;
using DCL.Chat.Commands;
using DCL.Chat.MessageBus;
using Segment.Serialization;
using System;
using System.Text.RegularExpressions;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class ChatAnalytics: IDisposable
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

        public void Dispose()
        {
            chatController.ChatBubbleVisibilityChanged -= OnChatBubbleVisibilityChanged;
            chatMessagesBus.MessageSent -= OnMessageSent;
            teleportToCommand.Executed -= OnTeleportedViaGoTo;
        }

        private void OnMessageSent(string message)
        {
            analytics.Track(AnalyticsEvents.Chat.MESSAGE_SENT, new JsonObject
            {
                { "message", message },
                { "emojis_amount", EMOJI_PATTERN.Matches(message).Count },
            });
        }

        private void OnTeleportedViaGoTo()
        {
            analytics.Track(AnalyticsEvents.Chat.GOTO_TELEPORT);
        }

        private void OnChatBubbleVisibilityChanged(bool isVisible)
        {
            // Skip initialization of chat bubble visibility
            if (isInitChatBubble)
            {
                isInitChatBubble = false;
                return;
            }

            analytics.Track(AnalyticsEvents.Chat.BUBBLE_SWITCHED, new JsonObject
            {
                { "is_visible", isVisible },
            });
        }
    }
}
