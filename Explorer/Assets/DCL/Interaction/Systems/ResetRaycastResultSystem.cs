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
    ///     Resets the raycast results stored in the SceneEntities and GlobalEntities caches if the entity is marked for deletion
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ResetRaycastResultSystem : BaseUnityLoopSystem
    {
        private ResetRaycastResultSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResetRaycastResultsQuery(World);
        }

        [Query]
        private void ResetRaycastResults(ref PlayerOriginRaycastResultForSceneEntities sceneEntities, ref PlayerOriginRaycastResultForGlobalEntities globalEntities)
        {
            var globalEntity = globalEntities.GetEntityInfo();
            if (globalEntity is not null && World.Has<DeleteEntityIntention>(globalEntity.Value.EntityReference))
                globalEntities.Reset();
        }
    }
}
