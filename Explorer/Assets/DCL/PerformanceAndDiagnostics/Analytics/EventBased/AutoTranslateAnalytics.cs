using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Translation.Events;
using DCL.Translation.Settings;
using Segment.Serialization;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class AutoTranslateAnalytics : IDisposable
    {
        private const string TRANSLATION_SETTINGS_CHANGE_EVENT = "TranslationSettingsChangeEvent";

        private readonly IAnalyticsController analytics;
        private readonly ITranslationSettings translationSettings;

        private readonly EventSubscriptionScope scope;

        private string currentChannelId = string.Empty;
        private ChatChannel.ChatChannelType currentChannelType = ChatChannel.ChatChannelType.UNDEFINED;

        public AutoTranslateAnalytics(IAnalyticsController analytics, IEventBus eventBus, ITranslationSettings translationSettings)
        {
            this.analytics = analytics;
            this.translationSettings = translationSettings;

            scope = new EventSubscriptionScope();
            scope.Add(eventBus.Subscribe<string>(OnTranslationSettingsChanged));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationRequested>(_ => { analytics.Track(AnalyticsEvents.AutoTranslate.MANUAL_MESSAGE_TRANSLATED); }));
            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationReverted>(_ => { analytics.Track(AnalyticsEvents.AutoTranslate.SHOW_ORIGINAL_MESSAGE); }));
            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(eventBus.Subscribe<TranslationEvents.ConversationAutoTranslateToggled>(OnAutoTranslateToggled));
        }

        private void OnTranslationSettingsChanged(string eventId)
        {
            if (eventId != TRANSLATION_SETTINGS_CHANGE_EVENT) return;

            var props = new JsonObject
            {
                { "language", translationSettings.PreferredLanguage.ToString() },
            };

            analytics.Track(AnalyticsEvents.AutoTranslate.CHOSEN_LANGUAGE, props);
        }

        public void Dispose()
        {
            scope.Dispose();
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            currentChannelId = evt.Channel.Id.Id;
            currentChannelType = evt.Channel.ChannelType;
        }

        private void OnAutoTranslateToggled(TranslationEvents.ConversationAutoTranslateToggled evt)
        {
            var props = new JsonObject
            {
                { "scope", ResolveScope(evt.ConversationId).ToString() },
                { "is_enabled", evt.IsEnabled },
            };

            analytics.Track(AnalyticsEvents.AutoTranslate.SWITCH_AUTOTRANSLATE, props);
        }

        private ChatChannel.ChatChannelType ResolveScope(string conversationId)
        {
            if (!string.IsNullOrEmpty(currentChannelId) && conversationId == currentChannelId)
                return currentChannelType;

            if (ChatChannel.IsCommunityChannelId(conversationId))
                return ChatChannel.ChatChannelType.COMMUNITY;

            if (conversationId == ChatChannel.NEARBY_CHANNEL_ID.Id)
                return ChatChannel.ChatChannelType.NEARBY;

            return ChatChannel.ChatChannelType.USER;
        }
    }
}
