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

        enum AssetBundleStatus
        {
            COMPLETE,
            FALLBACK
        }

        bool IsPlayerStandingOnScene { get; }

        /// <returns>it's null in a case the player is not standing on any scene</returns>
        IReadonlyReactiveProperty<RunningStatus?> SceneStatus { get; }

        /// <returns>it's null in a case the player is not standing on any scene</returns>
        IReadonlyReactiveProperty<AssetBundleStatus?> SceneAssetBundleStatus { get; }
    }
}
