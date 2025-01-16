using DCL.Utilities;
using SceneRunner.Scene;
using System;

namespace ECS.SceneLifeCycle.CurrentScene
{
    public class CurrentSceneInfo : ICurrentSceneInfo
    {
        private readonly ReactiveProperty<ICurrentSceneInfo.RunningStatus?> status = new (null);
        private readonly ReactiveProperty<ICurrentSceneInfo.AssetBundleStatus?> assetBundleStatus = new (null);


        public bool IsPlayerStandingOnScene { get; private set; }

        public IReadonlyReactiveProperty<ICurrentSceneInfo.RunningStatus?> SceneStatus => status;
        public IReadonlyReactiveProperty<ICurrentSceneInfo.AssetBundleStatus?> SceneAssetBundleStatus => assetBundleStatus;


        public void Update(ISceneFacade? sceneFacade)
        {
            IsPlayerStandingOnScene = sceneFacade != null;
            status.UpdateValue(StatusFrom(sceneFacade));
            assetBundleStatus.UpdateValue(AssetBundleStatusFrom(sceneFacade));
        }

        private static ICurrentSceneInfo.AssetBundleStatus? AssetBundleStatusFrom(ISceneFacade? sceneFacade)
        {
            if (sceneFacade == null)
                return null;

            return !string.IsNullOrEmpty(sceneFacade.SceneData.SceneEntityDefinition.status) && sceneFacade.SceneData.SceneEntityDefinition.status == "complete"
                ? ICurrentSceneInfo.AssetBundleStatus.COMPLETE
                : ICurrentSceneInfo.AssetBundleStatus.FALLBACK;
        }

        private static ICurrentSceneInfo.RunningStatus? StatusFrom(ISceneFacade? sceneFacade)
        {
            if (sceneFacade == null)
                return null;

            return sceneFacade.SceneStateProvider.IsNotRunningState()
                ? ICurrentSceneInfo.RunningStatus.Crashed
                : ICurrentSceneInfo.RunningStatus.Good;
        }
    }
}
