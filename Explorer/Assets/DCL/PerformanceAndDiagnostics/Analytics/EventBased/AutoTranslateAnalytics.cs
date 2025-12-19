using System;
using DCL.Chat;
using DCL.Chat.History;
using DCL.Translation;
using Newtonsoft.Json.Linq;
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

            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(eventBus.Subscribe<TranslationEvents.ConversationAutoTranslateToggled>(OnAutoTranslateToggled));

            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationReverted>(_ => { analytics.Track(AnalyticsEvents.AutoTranslate.SEE_ORIGINAL_MESSAGE); }));

            scope.Add(eventBus.Subscribe<TranslationEvents.MessageTranslationRequested>(_ =>
            {
                analytics.Track(AnalyticsEvents.AutoTranslate.TRANSLATE_MESSAGE_MANUALLY, new JObject
                {
                    { "language_chosen", translationSettings.PreferredLanguage.ToString() },
                });
            }));

        }

        public void Dispose()
        {
            scope.Dispose();
        }

        private void OnTranslationSettingsChanged(string eventId)
        {
            if (eventId != TRANSLATION_SETTINGS_CHANGE_EVENT)
                return;

            analytics.Track(AnalyticsEvents.AutoTranslate.CHOOSE_PREFERRED_LANGUAGE, new JObject
            {
                { "language_chosen", translationSettings.PreferredLanguage.ToString() },
            });
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            currentChannelId = evt.Channel.Id.Id;
            currentChannelType = evt.Channel.ChannelType;
        }

        private void OnAutoTranslateToggled(TranslationEvents.ConversationAutoTranslateToggled evt)
        {
            ChatChannel.ChatChannelType scope = ResolveScope(evt.ConversationId);

            var props = new JObject
            {
                { "enabled", evt.IsEnabled },
                { "scope", scope.ToString() },
            };

            if (scope == ChatChannel.ChatChannelType.USER)
                props.Add("receiver_id", evt.ConversationId);
            else if (scope == ChatChannel.ChatChannelType.COMMUNITY)
                props.Add("community_id", evt.ConversationId);

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
