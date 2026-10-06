using DCL.NotificationsBus;
using JetBrains.Annotations;
using SceneRunner.Scene;
using System.Threading;

namespace SceneRuntime.Apis.Modules.BadgesApi
{
    public class BadgesApiWrapper : JsApiWrapper
    {
        private readonly ISceneBadgesAwardChecker awardChecker;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ISceneData sceneData;

        public BadgesApiWrapper(ISceneBadgesAwardChecker awardChecker, ISceneStateProvider sceneStateProvider, ISceneData sceneData, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.awardChecker = awardChecker;
            this.sceneStateProvider = sceneStateProvider;
            this.sceneData = sceneData;
        }

        /// <summary>
        ///     A hint with no data: the current scene (never a portable experience, which is always "current")
        ///     says an award may have landed, and the checker re-reads the player's own awards.
        /// </summary>
        [PublicAPI("Used by StreamingAssets/Js/Modules/Badges.js")]
        public void CheckAwards()
        {
            if (!sceneStateProvider.IsCurrent || sceneData.IsPortableExperience())
                return;

            awardChecker.RequestCheck();
        }
    }
}
