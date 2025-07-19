using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Handles cleanup of GLTF Node material modifiers when removed or cleanup intention is added
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(UpdateGltfNodeModifierSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class CleanupGltfNodeModifierSystem : GltfNodeModifierSystemBase
    {
        public CleanupGltfNodeModifierSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            HandleGltfNodeModifiersRemovalQuery(World);
            HandleGltfNodeModifiersCleanupQuery(World);
        }

        [Query]
        [All(typeof(PBGltfNodeModifiers), typeof(GltfNodeModifiersCleanupIntention), typeof(Components.GltfNodeModifiers))]
        private void HandleGltfNodeModifiersCleanup(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            CleanupAllGltfNodeEntities(containerEntity, in gltfContainer);

            World.Remove<GltfNodeModifiersCleanupIntention>(containerEntity);
        }

        [Query]
        [All(typeof(Components.GltfNodeModifiers))]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleGltfNodeModifiersRemoval(Entity containerEntity, in GltfContainerComponent gltfContainer)
        {
            CleanupAllGltfNodeEntities(containerEntity, in gltfContainer);

            if (World.Has<GltfNodeModifiersCleanupIntention>(containerEntity))
                World.Remove<GltfNodeModifiersCleanupIntention>(containerEntity);
            World.Remove<Components.GltfNodeModifiers>(containerEntity);
        }
    }
}
