using DCL.Ipfs;
using DCL.Utilities;

namespace ECS.SceneLifeCycle.CurrentScene
{
    public interface ICurrentSceneInfo
    {
        enum RunningStatus
        {
            Good,
            Crashed,
        }

        bool IsPlayerStandingOnScene { get; }

        /// <returns>it's null in a case the player is not standing on any scene</returns>
        IReadonlyReactiveProperty<RunningStatus?> SceneStatus { get; }

        /// <returns>it's null in a case the player is not standing on any scene</returns>
        IReadonlyReactiveProperty<AssetBundleRegistryEnum?> SceneAssetBundleStatus { get; }
    }
}
