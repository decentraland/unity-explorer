using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DCL.SceneLoadingScreens
{
    public class FilterTipsFromFeatureFlag : ISceneTipsProvider
    {
        private readonly FeatureFlagsConfiguration featureFlags;
        private readonly ISceneTipsProvider source;
        private readonly List<SceneTips.Tip> filteredTipList = new ();
        private Tips tipsJson;
        private bool featureFlagChecked;
        private bool tipsParseSuccess;

        public FilterTipsFromFeatureFlag(FeatureFlagsConfiguration featureFlags,
            ISceneTipsProvider source)
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
                featureFlagChecked = true;
            }

            if (!tipsParseSuccess) return originTips;

            filteredTipList.Clear();
            SceneTips newTips = new SceneTips(originTips.Duration, originTips.Random, filteredTipList);
            filteredTipList.AddRange(originTips.Tips.Where(t => Contains(tipsJson, t)));

            return newTips;

            bool Contains(Tips tips, SceneTips.Tip tip) =>
                tips.displayed.Any(title => string.Equals(title, tip.Title, StringComparison.OrdinalIgnoreCase));
        }

        [Serializable]
        private struct Tips
        {
            public string[] displayed;
        }
    }
}
