using Arch.Core;
using DCL.Interaction.PlayerOriginated.Components;
using UnityEngine;

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
        private readonly Entity playerEntity;

        public PlayerInteractionEntity(Entity entity, World globalWorld, Entity playerEntity)
        {
            Entity = entity;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public ref PlayerOriginRaycastResultForSceneEntities PlayerOriginRaycastResultForSceneEntities => ref globalWorld.Get<PlayerOriginRaycastResultForSceneEntities>(Entity);
        public ref PlayerOriginRaycastResultForGlobalEntities PlayerOriginRaycastResultForGlobalEntities => ref globalWorld.Get<PlayerOriginRaycastResultForGlobalEntities>(Entity);
        public Vector3 PlayerPosition =>
            // TODO: find a better way to get the position?
            globalWorld.Get<CharacterController>(playerEntity).transform.position;
    }
}
