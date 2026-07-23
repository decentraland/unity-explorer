using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace DCL.SceneLoadingScreens
{
    public class TipsFromFeatureFlagDecorator : ISceneTipsProvider
    {
        private const int RETURNING_USER_THRESHOLD = 3;

        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly ISceneTipsProvider legacyTips;
        private readonly List<SceneTips.Tip> filteredTipList = new ();
        private Tips tipsJson;
        private AudienceTips audienceTipsJson;
        private TemporalTips temporalTipsJson;
        private bool featureFlagChecked;
        private bool tipsParseSuccess;
        private bool temporalTipsParseSuccess;
        private bool audienceTipsParseSuccess;

        public TipsFromFeatureFlagDecorator(ISceneTipsProvider legacyTips)
        {
            this.featureFlags = FeatureFlagsConfiguration.Instance;
            this.legacyTips = legacyTips;
        }

        public async UniTask<SceneTips> GetAsync(CancellationToken ct)
        {
            if (!featureFlagChecked)
            {
                audienceTipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.AUDIENCE_LOADING_SCREEN_TIPS, "tips", out audienceTipsJson);

                //TODO: remove all processing related to LOADING_SCREEN_TIPS feature flag when TEMPORAL_LOADING_SCREEN_TIPS is fully live
                tipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.LOADING_SCREEN_TIPS, "tips", out tipsJson);
                temporalTipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.TEMPORAL_LOADING_SCREEN_TIPS, "main", out temporalTipsJson);
                featureFlagChecked = true;
            }

            SceneTips originTips = await legacyTips.GetAsync(ct);

            if (audienceTipsParseSuccess)
            {
                filteredTipList.Clear();

                SceneTips audienceTips = new (originTips.Duration, originTips.Random, filteredTipList);

                filteredTipList.AddRange(LaunchCounter.Count >= RETURNING_USER_THRESHOLD
                    ? ToPreConfiguredTip(audienceTipsJson.returningUsers)
                    : ToPreConfiguredTip(audienceTipsJson.newUsers));

                return audienceTips;
            }

            if (!tipsParseSuccess && !temporalTipsParseSuccess) return originTips;

            filteredTipList.Clear();
            SceneTips newTips = new SceneTips(originTips.Duration, originTips.Random, filteredTipList);

            filteredTipList.AddRange(temporalTipsParseSuccess ? originTips.Tips.Where(t => Contains(temporalTipsJson, t)) : originTips.Tips.Where(t => Contains(tipsJson, t)));

            return newTips;
        }

        private bool Contains(Tips tips, SceneTips.Tip tip) =>
            tips.displayed.Any(title => string.Equals(title, tip.Title, StringComparison.OrdinalIgnoreCase));

        private bool Contains(TemporalTips tips, SceneTips.Tip tip) =>
            tips.displayed.Any(temporalTip => string.Equals(temporalTip.name, tip.Title, StringComparison.OrdinalIgnoreCase)
                                              && temporalTip.IsActive());

        private IEnumerable<SceneTips.Tip> ToPreConfiguredTip(Tips tips) =>
            tips.displayed.Select(title => new SceneTips.Tip(title, "", null)).ToList();

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
