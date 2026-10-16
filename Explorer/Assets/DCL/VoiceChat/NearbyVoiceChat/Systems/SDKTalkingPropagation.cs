using Arch.Core;
using DCL.Multiplayer.SDK.Components;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Writes a remote player's talking state to the <see cref="SDKTalking" /> of the scene the player is assigned to.
    /// </summary>
    public static class SDKTalkingPropagation
    {
        public static void Propagate(World world, Entity entity, bool talking)
        {
            if (world.TryGet(entity, out PlayerCRDTEntity playerCRDTEntity))
                Propagate(in playerCRDTEntity, talking);
        }

        public static void Propagate(in PlayerCRDTEntity playerCRDTEntity, bool talking)
        {
            if (playerCRDTEntity.SceneFacade is not { } sceneFacade) return;

            ref SDKTalking sdkTalking = ref sceneFacade.EcsExecutor.World.TryGetRef<SDKTalking>(playerCRDTEntity.SceneWorldEntity, out bool exists);

            if (exists)
                sdkTalking.Set(talking);
        }
    }
}
