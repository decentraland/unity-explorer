using DCL.Chat.ChatReactions;
using DCL.Settings.Settings;
using Newtonsoft.Json.Linq;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class ChatReactionsAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly ChatMessageReactionService messageReactionService;
        private readonly SituationalReactionFacade situationalReactionFacade;
        private readonly ChatSettingsAsset chatSettingsAsset;

        public ChatReactionsAnalytics(
            IAnalyticsController analytics,
            ChatMessageReactionService messageReactionService,
            SituationalReactionFacade situationalReactionFacade,
            ChatSettingsAsset chatSettingsAsset)
        {
            this.analytics = analytics;
            this.messageReactionService = messageReactionService;
            this.situationalReactionFacade = situationalReactionFacade;
            this.chatSettingsAsset = chatSettingsAsset;

            messageReactionService.UserReactedToMessage += OnMessageReaction;
            situationalReactionFacade.UserReactedToSituation += OnSituationalReaction;
            chatSettingsAsset.ChatReactionsEnabledChanged += OnVisualizationToggled;
        }

        public void Dispose()
        {
            messageReactionService.UserReactedToMessage -= OnMessageReaction;
            situationalReactionFacade.UserReactedToSituation -= OnSituationalReaction;
            chatSettingsAsset.ChatReactionsEnabledChanged -= OnVisualizationToggled;
        }

        private void OnMessageReaction(int emojiIndex, bool isParticipation)
        {
            analytics.Track(AnalyticsEvents.Reactions.REACT_CHAT_MESSAGE, new JObject
            {
                { "emoji_id", emojiIndex },
                { "source", isParticipation ? "participated" : "generated" },
            });
        }

        private void OnSituationalReaction(int emojiIndex)
        {
            analytics.Track(AnalyticsEvents.Reactions.REACT_SITUATION, new JObject
            {
                { "reaction_id", emojiIndex },
            });
        }

        private void OnVisualizationToggled(bool enabled)
        {
            analytics.Track(AnalyticsEvents.Reactions.REACTION_VISUALIZATION, new JObject
            {
                { "state", enabled },
            });
        }
    }
}
