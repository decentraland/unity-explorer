using System;
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

        private readonly IDisposable? subscription;
        private readonly IDisposable? manualTranslateSubscription;
        private readonly IDisposable? revertTranslateSubscription;

        public AutoTranslateAnalytics(IAnalyticsController analytics, IEventBus eventBus, ITranslationSettings translationSettings)
        {
            this.analytics = analytics;
            this.translationSettings = translationSettings;

            subscription = eventBus.Subscribe<string>(OnTranslationSettingsChanged);

            manualTranslateSubscription = eventBus.Subscribe<TranslationEvents.MessageTranslationRequested>(_ => { analytics.Track(AnalyticsEvents.AutoTranslate.MANUAL_MESSAGE_TRANSLATED); });

            revertTranslateSubscription = eventBus.Subscribe<TranslationEvents.MessageTranslationReverted>(_ => { analytics.Track(AnalyticsEvents.AutoTranslate.SHOW_ORIGINAL_MESSAGE); });
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
            subscription?.Dispose();
            manualTranslateSubscription?.Dispose();
            revertTranslateSubscription?.Dispose();
        }
    }
}
