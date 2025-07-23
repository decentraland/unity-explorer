using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.GltfNodeModifiers.Components;
using UnityEngine;
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
        private readonly EntityEventBuffer<GltfContainerComponent> changedGltfs;
        private readonly EntityEventBuffer<GltfContainerComponent>.ForEachDelegate eventHandler;

        public CleanupGltfNodeModifierSystem(World world, EntityEventBuffer<GltfContainerComponent> changedGltfs) : base(world)
        {
            this.changedGltfs = changedGltfs;
            eventHandler = HandleGltfContainerChange;
        }

        protected override void Update(float t)
        {
            changedGltfs.ForEach(eventHandler);

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            HandleGltfContainerComponentRemovalQuery(World);
            HandleCleanupIntentionQuery(World);
        }

        [Query]
        [All(typeof(PBGltfNodeModifiers), typeof(GltfNodeModifiersCleanupIntention))]
        private void HandleCleanupIntention(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            RunCleanup(containerEntity, ref nodeModifiers);
        }

        [Query]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleComponentRemoval(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            RunCleanup(containerEntity, ref nodeModifiers);
        }

        [Query]
        [All(typeof(GltfContainerComponent))]
        [None(typeof(PBGltfContainer))]
        private void HandleGltfContainerComponentRemoval(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            RunCleanup(containerEntity, ref nodeModifiers);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            RunCleanup(containerEntity, ref nodeModifiers);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        [Query]
        private void FinalizeComponents(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            RunCleanup(containerEntity, ref nodeModifiers);
        }

        private void RunCleanup(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            CleanupAllGltfNodeEntities(containerEntity, ref nodeModifiers);

            // Reset all renderers to their original materials
            ResetOriginalMaterials(nodeModifiers);

            ListPool<Entity>.Release(nodeModifiers.GltfNodeEntities);
            DictionaryPool<Renderer, Material>.Release(nodeModifiers.OriginalMaterials);

            World.TryRemove<GltfNodeModifiersCleanupIntention>(containerEntity);
            World.TryRemove<Components.GltfNodeModifiers>(containerEntity);
        }

        /// <summary>
        ///     Resets all renderers to their original materials and shadow casting state
        /// </summary>
        private static void ResetOriginalMaterials(Components.GltfNodeModifiers nodeModifiers)
        {
            foreach (var rendererMaterialKeyValuePair in nodeModifiers.OriginalMaterials)
            {
                rendererMaterialKeyValuePair.Key.sharedMaterial = rendererMaterialKeyValuePair.Value;
                rendererMaterialKeyValuePair.Key.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            nodeModifiers.OriginalMaterials.Clear();
        }

        /// <summary>
        ///     Runs cleanup on invalidated GLTFs (ResetGltfContainerSystem)
        /// </summary>
        private void HandleGltfContainerChange(Entity entity, GltfContainerComponent component)
        {
            var nodeModifiers = World.TryGetRef<Components.GltfNodeModifiers>(entity, out bool exists);
            if (!exists) return;

            RunCleanup(entity, ref nodeModifiers);
        }
    }
}
