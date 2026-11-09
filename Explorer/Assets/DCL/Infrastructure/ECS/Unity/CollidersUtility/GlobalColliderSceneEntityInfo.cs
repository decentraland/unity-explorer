using Arch.Core;
using DCL.ECSComponents;
using SceneRunner.Scene;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace DCL.Interaction.Utility
{
    /// <summary>
    /// Used for detecting colliders in the scene world (like for example any interactable object of a scene).
    /// </summary>
    public readonly struct GlobalColliderSceneEntityInfo
    {
        public readonly SceneEcsExecutor EcsExecutor;
        public readonly ColliderSceneEntityInfo ColliderSceneEntityInfo;

        public GlobalColliderSceneEntityInfo(SceneEcsExecutor ecsExecutor, ColliderSceneEntityInfo colliderSceneEntityInfo)
        {
            EcsExecutor = ecsExecutor;
            ColliderSceneEntityInfo = colliderSceneEntityInfo;
        }

        [Pure]
        public bool TryGetPointerEvents([NotNullWhen(true)] out PBPointerEvents? pbPointerEvents)
        {
            World world = EcsExecutor.World;
            Entity entityRef = ColliderSceneEntityInfo.EntityReference;

            if (!world.IsAlive(entityRef))
            {
                pbPointerEvents = null;
                return false;
            }

            return world.TryGet(entityRef, out pbPointerEvents);
        }

        public bool IsSameEntity(in GlobalColliderSceneEntityInfo other) =>
            EcsExecutor.World == other.EcsExecutor.World
            && ColliderSceneEntityInfo.EntityReference == other.ColliderSceneEntityInfo.EntityReference;
    }
}
