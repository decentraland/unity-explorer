using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.GltfNodeModifiers.Components;
using UnityEngine.Pool;
using Utility.Arch;

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Handles cleanup of GLTF Node material modifiers when removed or cleanup intention is added
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [UpdateBefore(typeof(CleanUpGltfContainerSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CleanupGltfNodeModifierSystem : GltfNodeModifierSystemBase, IFinalizeWorldSystem
    {
        public CleanupGltfNodeModifierSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            HandleCleanupIntentionQuery(World);
        }

        [Query]
        [All(typeof(PBGltfNodeModifiers), typeof(GltfNodeModifiersCleanupIntention), typeof(Components.GltfNodeModifiers))]
        private void HandleCleanupIntention(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            RunCleanup(containerEntity, ref gltfContainer);
        }

        [Query]
        [All(typeof(Components.GltfNodeModifiers))]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleComponentRemoval(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            RunCleanup(containerEntity, ref gltfContainer);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(Components.GltfNodeModifiers))]
        private void HandleEntityDestruction(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            RunCleanup(containerEntity, ref gltfContainer);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        [All(typeof(Components.GltfNodeModifiers))]
        private void FinalizeComponents(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            RunCleanup(containerEntity, ref gltfContainer);
        }

        private void RunCleanup(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            CleanupAllGltfNodeEntities(containerEntity, in gltfContainer);

            ListPool<Entity>.Release(gltfContainer.GltfNodeEntities);

            World.TryRemove<GltfNodeModifiersCleanupIntention>(containerEntity);
            World.TryRemove<Components.GltfNodeModifiers>(containerEntity);
        }
    }
}
