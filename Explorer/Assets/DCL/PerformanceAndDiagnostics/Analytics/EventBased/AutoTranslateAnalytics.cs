using System;
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

        public AutoTranslateAnalytics(IAnalyticsController analytics, IEventBus eventBus, ITranslationSettings translationSettings)
        {
            this.analytics = analytics;
            this.translationSettings = translationSettings;

            subscription = eventBus.Subscribe<string>(OnTranslationSettingsChanged);
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
        }
    }
}
