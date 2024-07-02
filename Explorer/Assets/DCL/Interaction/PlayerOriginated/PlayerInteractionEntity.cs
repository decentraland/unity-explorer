using Arch.Core;
using DCL.Interaction.PlayerOriginated.Components;

namespace DCL.Interaction.PlayerOriginated
{
    /// <summary>
    ///     Entity exists in a single instance and encapsulates information related to
    ///     the interaction provoked by the player itself
    /// </summary>
    public readonly struct PlayerInteractionEntity
    {
        public readonly Entity Entity;
        private readonly World globalWorld;

        public PlayerInteractionEntity(Entity entity, World globalWorld)
        {
            Entity = entity;
            this.globalWorld = globalWorld;
        }

        public ref PlayerOriginRaycastResultForSceneEntities PlayerOriginRaycastResultForSceneEntities => ref globalWorld.Get<PlayerOriginRaycastResultForSceneEntities>(Entity);
        public ref PlayerOriginRaycastResultForGlobalEntities PlayerOriginRaycastResultForGlobalEntities => ref globalWorld.Get<PlayerOriginRaycastResultForGlobalEntities>(Entity);
    }
}
