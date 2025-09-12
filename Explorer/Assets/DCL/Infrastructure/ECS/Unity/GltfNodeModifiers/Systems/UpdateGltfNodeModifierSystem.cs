using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Handles updates to GLTF Node material modifiers when PBGltfNodeModifiers changes
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [UpdateAfter(typeof(SetupGltfNodeModifierSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class UpdateGltfNodeModifierSystem : GltfNodeModifierSystemBase
    {
        private readonly Dictionary<string, Entity> auxiliaryGltfNodePathEntities = new Dictionary<string, Entity>();

        public UpdateGltfNodeModifierSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateGltfNodesQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(Components.GltfNodeModifiers))]
        private void UpdateGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, ref Components.GltfNodeModifiers nodeModifiers, in PartitionComponent partitionComponent)
        {
            if (!gltfNodeModifiers.IsDirty || !IsGltfContainerReady(ref gltfContainer, out StreamableLoadingResult<GltfContainerAsset> result))
                return;

            gltfNodeModifiers.IsDirty = false;

            if (gltfNodeModifiers.Modifiers.Count == 0)
            {
                CleanupAllGltfNodeEntities(entity, ref nodeModifiers);
                return;
            }

            // Special case: single modifier with empty path applies to ALL renderers
            if (IsGltfGlobalModifier(gltfNodeModifiers.Modifiers))
                UpdateGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref nodeModifiers, result.Asset!, partitionComponent);
            else
                UpdateIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref nodeModifiers, partitionComponent, result.Asset!);
        }

        /// <summary>
        ///     Updates the global modifier (single modifier with empty path)
        /// </summary>
        private void UpdateGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref Components.GltfNodeModifiers nodeModifiers, GltfContainerAsset asset, in PartitionComponent partitionComponent)
        {
            // Check if transitioning from individual modifiers to global modifier
            if (nodeModifiers.GltfNodeEntities is { Count: > 0 } &&
                (nodeModifiers.GltfNodeEntities.Count > 1 || !nodeModifiers.GltfNodeEntities.ContainsKey(containerEntity)))
            {
                // Clean up individual modifier entities
                foreach (Entity entityToCleanup in nodeModifiers.GltfNodeEntities.Keys) { CleanupGltfNodeEntity(entityToCleanup, containerEntity); }

                nodeModifiers.GltfNodeEntities.Clear();

                // Add global GltfNode component to container entity if transitioning from individual
                if (!World.Has<GltfNode>(containerEntity)) { CreateGlobalGltfNode(containerEntity, asset); }

                nodeModifiers.GltfNodeEntities!.Add(containerEntity, string.Empty);
            }

            (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

            asset.SetCastingShadows(!hasShadowOverride || modifier.CastShadows);

            if (hasMaterialOverride) { AddOrUpdateMaterial(containerEntity, modifier.Material, partitionComponent); }
            else
            {
                if (World.Has<PBMaterial>(containerEntity))
                {
                    // Material was removed, clean it up but don't destroy entity
                    ref GltfNode gltfNode = ref World.TryGetRef<GltfNode>(containerEntity, out bool exists);
                    TriggerGltfNodeMaterialCleanup(containerEntity, ref gltfNode, false);
                }
            }
        }

        /// <summary>
        ///     Updates individual modifiers (multiple modifiers or specific paths)
        /// </summary>
        private void UpdateIndividualModifiers(Entity containerEntity, IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers, ref Components.GltfNodeModifiers nodeModifiers, PartitionComponent partitionComponent, GltfContainerAsset asset)
        {
            if (nodeModifiers.GltfNodeEntities.Count == 0) return;

            // Check if transitioning from global modifier to individual modifiers
            if (nodeModifiers.GltfNodeEntities.Count == 1 && nodeModifiers.GltfNodeEntities.ContainsKey(containerEntity))
            {
                CleanupGltfNodeEntity(containerEntity, containerEntity);
                nodeModifiers.GltfNodeEntities.Remove(containerEntity);
            }

            // Check previously existent GltfNode entities to recycle and remove as needed
            foreach (var kvp in nodeModifiers.GltfNodeEntities)
            {
                auxiliaryGltfNodePathEntities[kvp.Value] = kvp.Key;
            }

            foreach (PBGltfNodeModifiers.Types.GltfNodeModifier? modifier in modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

                if (auxiliaryGltfNodePathEntities.TryGetValue(modifier.Path, out Entity existingEntity))
                {
                    UpdateExistingGltfNodeEntity(existingEntity, modifier, hasShadowOverride, hasMaterialOverride, partitionComponent);
                    auxiliaryGltfNodePathEntities.Remove(modifier.Path); // Mark as processed
                }
                else
                {
                    CreateNewGltfNodeEntity(containerEntity, modifier, asset.Root.transform, ref nodeModifiers, partitionComponent, hasShadowOverride, hasMaterialOverride, asset.HierarchyPaths);
                }
            }

            // Clean up entities that no longer have corresponding modifiers
            foreach (Entity orphanedEntity in auxiliaryGltfNodePathEntities.Values)
            {
                CleanupGltfNodeEntity(orphanedEntity, containerEntity);
                nodeModifiers.GltfNodeEntities!.Remove(orphanedEntity);
            }
            auxiliaryGltfNodePathEntities.Clear();
        }

        /// <summary>
        ///     Updates an existing GltfNode entity with new modifier data
        /// </summary>
        private void UpdateExistingGltfNodeEntity(Entity nodeEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, bool hasShadowOverride, bool hasMaterialOverride, PartitionComponent partitionComponent)
        {
            ref GltfNode gltfNode = ref World.TryGetRef<GltfNode>(nodeEntity, out bool exists);

            foreach (Renderer? renderer in gltfNode.Renderers)
                renderer.shadowCastingMode = !hasShadowOverride || modifier.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (hasMaterialOverride) { AddOrUpdateMaterial(nodeEntity, modifier.Material, partitionComponent); }
            else if (World.Has<PBMaterial>(nodeEntity))
            {
                // Material was removed, clean it up but don't destroy entity
                TriggerGltfNodeMaterialCleanup(nodeEntity, ref gltfNode, false);
            }
        }
    }
}
