using Arch.Core;
using DCL.Multiplayer.SDK.Components;
using DCL.Optimization.Pools;
using DCL.Profiles;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    /// <summary>
    ///     Propagates data related to the character from the global world to the scene world
    /// </summary>
    public class CharacterDataPropagationUtility
    {
        private readonly IComponentPool<SDKProfile> profileSDKSubProductPool;

        public CharacterDataPropagationUtility(IComponentPool<SDKProfile> componentPool)
        {
            profileSDKSubProductPool = componentPool;
        }

        public void CopyProfileToSceneEntity(Profile profile, World sceneWorld, Entity sceneEntity)
        {
            if (!sceneWorld.TryGet(sceneEntity, out SDKProfile? profileSDKSubProduct))
            {
                profileSDKSubProduct = profileSDKSubProductPool.Get();
                sceneWorld.Add(sceneEntity, profileSDKSubProduct);
            }

            profileSDKSubProduct!.OverrideWith(profile);
        }
    }
}
