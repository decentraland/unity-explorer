using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Interaction.PlayerOriginated.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Interaction.Raycast
{
    /// <summary>
    ///     Resets the raycast result cache for global entities when they are marked for deletion.
    ///     Runs in CleanUpGroup to ensure cleanup happens before entities are destroyed.
    /// </summary>
    /// <remarks>
    ///     Only resets GlobalEntities results, not SceneEntities, since only global entities (like avatars)
    ///     are tracked for hover state and need cleanup. Scene raycast results are discarded each frame anyway.
    /// </remarks>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ResetRaycastResultSystem : BaseUnityLoopSystem
    {
        private ResetRaycastResultSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResetRaycastResultsQuery(World);
        }

        /// <summary>
        /// Finds all entities with raycast results and clears the global entity cache if that entity is being deleted.
        /// </summary>
        [Query]
        private void ResetRaycastResults(ref PlayerOriginRaycastResultForSceneEntities sceneEntities, ref PlayerOriginRaycastResultForGlobalEntities globalEntities)
        {
            var globalEntity = globalEntities.GetEntityInfo();
            if (globalEntity is not null && World.Has<DeleteEntityIntention>(globalEntity.Value.EntityReference))
                globalEntities.Reset();
        }
    }
}
