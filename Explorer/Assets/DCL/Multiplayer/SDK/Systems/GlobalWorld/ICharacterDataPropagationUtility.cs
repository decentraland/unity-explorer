using Arch.Core;
using DCL.Profiles;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    public interface ICharacterDataPropagationUtility
    {
        void CopyProfileToSceneEntity(Profile profile, SceneEcsExecutor sceneEcsExecutor, Entity sceneEntity);

        void PropagateGlobalPlayerToScenePlayer(World globalWorld, Entity globalPlayerEntity, ISceneFacade sceneFacade);
    }
}
