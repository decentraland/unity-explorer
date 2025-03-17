using DCL.Utilities;
using SceneRunner.Scene;
using System;
using DCL.Ipfs;

namespace ECS.SceneLifeCycle.CurrentScene
{
    public class CurrentSceneInfo : ICurrentSceneInfo
    {
        private readonly ReactiveProperty<ICurrentSceneInfo.RunningStatus?> status = new (null);
        private readonly ReactiveProperty<AssetBundleRegistryEnum?> assetBundleStatus = new (null);


        public bool IsPlayerStandingOnScene { get; private set; }

        public IReadonlyReactiveProperty<ICurrentSceneInfo.RunningStatus?> SceneStatus => status;
        public IReadonlyReactiveProperty<AssetBundleRegistryEnum?> SceneAssetBundleStatus => assetBundleStatus;


        public void Update(ISceneFacade? sceneFacade)
        {
            IsPlayerStandingOnScene = sceneFacade != null;
            status.UpdateValue(StatusFrom(sceneFacade));
            assetBundleStatus.UpdateValue(AssetBundleStatusFrom(sceneFacade));
        }

        private static AssetBundleRegistryEnum? AssetBundleStatusFrom(ISceneFacade? sceneFacade)
        {
            if (sceneFacade == null)
                return null;

            return sceneFacade.SceneData.SceneEntityDefinition.assetBundleRegistryEnum;
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
