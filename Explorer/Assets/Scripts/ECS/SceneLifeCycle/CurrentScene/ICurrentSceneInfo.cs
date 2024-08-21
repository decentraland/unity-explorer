using DCL.Utilities;

namespace ECS.SceneLifeCycle.CurrentScene
{
    public interface ICurrentSceneInfo
    {
        enum Status
        {
            Good,
            Crashed,
        }

        bool IsPlayerStandingOnScene { get; }

        /// <returns>it's null in a case the player is not standing on any scene</returns>
        IReadonlyReactiveProperty<Status?> SceneStatus { get; }
    }
}
