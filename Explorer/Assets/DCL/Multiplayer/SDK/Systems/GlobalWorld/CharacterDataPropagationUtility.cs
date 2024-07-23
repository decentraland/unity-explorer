using Arch.Core;
using CrdtEcsBridge.Components;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using DCL.Profiles;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    /// <summary>
    ///     Propagates data related to the character from the global world to the scene world
    /// </summary>
    public class CharacterDataPropagationUtility : ICharacterDataPropagationUtility
    {
        private readonly IComponentPool<SDKProfile> profileSDKSubProductPool;

        public CharacterDataPropagationUtility(IComponentPool<SDKProfile> componentPool)
        {
            profileSDKSubProductPool = componentPool;
        }

        public void PropagateGlobalPlayerToScenePlayer(World globalWorld, Entity globalPlayerEntity, ISceneFacade sceneFacade)
        {
            Entity targetEntity = sceneFacade.PersistentEntities.Player;

            CopyProfileToSceneEntity(globalWorld.Get<Profile>(globalPlayerEntity), sceneFacade.EcsExecutor, targetEntity);
            sceneFacade.EcsExecutor.World.Add(targetEntity, new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
        }

        public void CopyProfileToSceneEntity(Profile profile, SceneEcsExecutor sceneEcsExecutor, Entity sceneEntity)
        {
            if (!sceneEcsExecutor.World.TryGet(sceneEntity, out SDKProfile? profileSDKSubProduct))
            {
                profileSDKSubProduct = profileSDKSubProductPool.Get();
                sceneEcsExecutor.World.Add(sceneEntity, profileSDKSubProduct);
            }

            profileSDKSubProduct!.OverrideWith(profile);
        }
    }
}
