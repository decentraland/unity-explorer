using DCL.NotificationsBus;
using JetBrains.Annotations;
using SceneRunner.Scene;
using System.Threading;

namespace SceneRuntime.Apis.Modules.BadgesApi
{
    public class BadgesApiWrapper : JsApiWrapper
    {
        private readonly ISceneBadgesAwardCheck awardCheck;
        private readonly ISceneStateProvider sceneStateProvider;

        public BadgesApiWrapper(ISceneBadgesAwardCheck awardCheck, ISceneStateProvider sceneStateProvider, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.awardCheck = awardCheck;
            this.sceneStateProvider = sceneStateProvider;
        }

        [PublicAPI("Used by StreamingAssets/Js/Modules/Badges.js")]
        public void CheckAwards()
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            awardCheck.RequestCheck();
        }
    }
}
