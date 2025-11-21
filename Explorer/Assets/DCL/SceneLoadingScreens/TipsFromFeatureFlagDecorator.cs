using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace DCL.SceneLoadingScreens
{
    public class TipsFromFeatureFlagDecorator : ISceneTipsProvider
    {
        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly ISceneTipsProvider source;
        private readonly List<SceneTips.Tip> filteredTipList = new ();
        private Tips tipsJson;
        private TemporalTips temporalTipsJson;
        private bool featureFlagChecked;
        private bool tipsParseSuccess;
        private bool temporalTipsParseSuccess;

        public TipsFromFeatureFlagDecorator(ISceneTipsProvider source, FeatureFlagsConfiguration featureFlags)
        {
            this.featureFlags = featureFlags;
            this.source = source;
        }

        public async UniTask<SceneTips> GetAsync(CancellationToken ct)
        {
            SceneTips originTips = await source.GetAsync(ct);

            if (!featureFlagChecked)
            {
                tipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.LOADING_SCREEN_TIPS, "tips", out tipsJson);
                temporalTipsParseSuccess = featureFlags.TryGetJsonPayload(FeatureFlagsStrings.TEMPORAL_LOADING_SCREEN_TIPS, "tips", out temporalTipsJson);
                featureFlagChecked = true;
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

                    return DateTime.UtcNow.Date >= ProcessedStartDate.Date && DateTime.UtcNow.Date <= ProcessedEndDate.Date;
                }

                [OnDeserialized]
                public void OnAfterDeserialize(StreamingContext context)
                {
                    DateTime.TryParse(startDate, out ProcessedStartDate);
                    DateTime.TryParse(endDate, out ProcessedEndDate);
                }
            }
        }
    }
}
