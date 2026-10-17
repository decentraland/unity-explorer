using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace DCL.SceneLoadingScreens
{
    public class TipsFromFeatureFlagDecorator : ISceneTipsProvider
    {
        private const int RETURNING_USER_THRESHOLD = 3;

        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly ISceneTipsProvider legacyTips;
        private readonly LoadingTipCatalogSO tipCatalog;
        private readonly List<SceneTips.Tip> filteredTipList = new ();
        private Tips tipsJson;
        private AudienceTips audienceTipsJson;
        private TemporalTips temporalTipsJson;
        private bool featureFlagChecked;
        private bool tipsParseSuccess;
        private bool temporalTipsParseSuccess;
        private bool audienceTipsParseSuccess;

        public TipsFromFeatureFlagDecorator(ISceneTipsProvider legacyTips, LoadingTipCatalogSO tipCatalog)
        {
            this.featureFlags = FeatureFlagsConfiguration.Instance;
            this.legacyTips = legacyTips;
            this.tipCatalog = tipCatalog;
        }

        public SceneTips Get()
        {
            if (!featureFlagChecked)
            {
                audienceTipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.AUDIENCE_LOADING_SCREEN_TIPS, "tips", out audienceTipsJson);

                if (audienceTipsParseSuccess)
                {
                    // The flag may list tips added in newer builds; keep only the ones this build can render
                    audienceTipsJson.newUsers.displayed = KeepKnownTips(audienceTipsJson.newUsers.displayed);
                    audienceTipsJson.returningUsers.displayed = KeepKnownTips(audienceTipsJson.returningUsers.displayed);
                }

                //TODO: remove all processing related to LOADING_SCREEN_TIPS feature flag when TEMPORAL_LOADING_SCREEN_TIPS is fully live
                tipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.LOADING_SCREEN_TIPS, "tips", out tipsJson);
                temporalTipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.TEMPORAL_LOADING_SCREEN_TIPS, "main", out temporalTipsJson);
                featureFlagChecked = true;
            }

            SceneTips originTips = legacyTips.Get();

            if (audienceTipsParseSuccess)
            {
                Tips tips = LaunchCounter.Count >= RETURNING_USER_THRESHOLD
                    ? audienceTipsJson.returningUsers
                    : audienceTipsJson.newUsers;

                // With no renderable tips left, fall through to the other sources instead of showing an empty screen
                if (tips.displayed.Length > 0)
                {
                    filteredTipList.Clear();

                    foreach (string key in tips.displayed)
                        filteredTipList.Add(new SceneTips.Tip(key));

                    return new SceneTips(originTips.Duration, originTips.Random, filteredTipList);
                }
            }

            if (!tipsParseSuccess && !temporalTipsParseSuccess) return originTips;

            filteredTipList.Clear();
            SceneTips newTips = new SceneTips(originTips.Duration, originTips.Random, filteredTipList);

            filteredTipList.AddRange(temporalTipsParseSuccess ? originTips.Tips.Where(t => Contains(temporalTipsJson, t)) : originTips.Tips.Where(t => Contains(tipsJson, t)));

            return newTips;
        }

        private string[] KeepKnownTips(string[]? keys)
        {
            if (keys == null) return Array.Empty<string>();

            var known = new List<string>(keys.Length);

            foreach (string key in keys)
            {
                if (tipCatalog.TryGet(key, out _))
                    known.Add(key);
                else
                    ReportHub.LogWarning(ReportCategory.UI, $"Loading screen tip '{key}' from feature flag {FeatureFlagsStrings.AUDIENCE_LOADING_SCREEN_TIPS} is not in this build's catalog, skipping it");
            }

            return known.ToArray();
        }

        private bool Contains(Tips tips, SceneTips.Tip tip) =>
            tips.displayed.Any(title => string.Equals(title, tip.Key, StringComparison.OrdinalIgnoreCase));

        private bool Contains(TemporalTips tips, SceneTips.Tip tip) =>
            tips.displayed.Any(temporalTip => string.Equals(temporalTip.name, tip.Key, StringComparison.OrdinalIgnoreCase)
                                              && temporalTip.IsActive());

        // ReSharper disable InconsistentNaming
        [Serializable]
        private struct Tips
        {
            public string[] displayed;
        }

        [Serializable]
        private struct TemporalTips
        {
            public TemporalTip[] displayed;

            [Serializable]
            public struct TemporalTip
            {
                public string name;
                public string startDate;
                public string endDate;

                public DateTime ProcessedStartDate;
                public DateTime ProcessedEndDate;

                public bool IsActive()
                {
                    if (string.IsNullOrEmpty(startDate) && string.IsNullOrEmpty(endDate)) return true;

                    return DateTime.UtcNow >= ProcessedStartDate && DateTime.UtcNow <= ProcessedEndDate;
                }

                [OnDeserialized]
                public void OnAfterDeserialize(StreamingContext context)
                {
                    DateTime.TryParse(startDate, out ProcessedStartDate);
                    DateTime.TryParse(endDate, out ProcessedEndDate);
                }
            }
        }

        [Serializable]
        private struct AudienceTips
        {
            public Tips newUsers;
            public Tips returningUsers;
        }
        // ReSharper restore InconsistentNaming
    }
}
