using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
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
        public UpdateGltfNodeModifierSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateGltfNodesQuery(World);
        }

        [Query]
        [All(typeof(Components.GltfNodeModifiers))]
        private void UpdateGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, in PartitionComponent partitionComponent)
        {
            if (!gltfNodeModifiers.IsDirty || !IsGltfContainerReady(ref gltfContainer, out StreamableLoadingResult<GltfContainerAsset> result))
                return;

            gltfNodeModifiers.IsDirty = false;

            if (gltfNodeModifiers.Modifiers.Count == 0)
            {
                CleanupAllGltfNodeEntities(entity, in gltfContainer);
                return;
            }

            // Special case: single modifier with empty path applies to ALL renderers
            if (IsGltfRootModifier(gltfNodeModifiers.Modifiers))
                UpdateGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref gltfContainer, result.Asset!);
            else
                UpdateIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref gltfContainer, partitionComponent);
        }

        /// <summary>
        ///     Updates the global modifier (single modifier with empty path)
        /// </summary>
        private void UpdateGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref GltfContainerComponent gltfContainer, GltfContainerAsset asset)
        {
            // Check if transitioning from individual modifiers to global modifier
            if (gltfContainer.GltfNodeEntities is { Count: > 0 } &&
                (gltfContainer.GltfNodeEntities.Count > 1 || gltfContainer.GltfNodeEntities[0] != containerEntity))
            {
                // Clean up individual modifier entities
                foreach (Entity entityToCleanup in gltfContainer.GltfNodeEntities) { CleanupGltfNodeEntity(entityToCleanup, containerEntity); }

                gltfContainer.GltfNodeEntities.Clear();

                // Add global GltfNode component to container entity if transitioning from individual
                if (!World.Has<GltfNode>(containerEntity)) { CreateGlobalGltfNode(containerEntity, asset); }

                gltfContainer.GltfNodeEntities!.Add(containerEntity);
            }

            (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

            asset.SetCastingShadows(!hasShadowOverride || modifier.CastShadows);

            if (hasMaterialOverride) { AddOrUpdateMaterial(containerEntity, modifier.Material); }
            else
            {
                if (World.Has<PBMaterial>(containerEntity))
                {
                    // Material was removed, clean it up but don't destroy entity
                    GltfNode gltfNode = World.Get<GltfNode>(containerEntity);
                    CreateMaterialCleanupIntention(containerEntity, gltfNode.Renderers, gltfNode.ContainerEntity, false);
                }
            }
        }

        /// <summary>
        ///     Updates individual modifiers (multiple modifiers or specific paths)
        /// </summary>
        private void UpdateIndividualModifiers(Entity containerEntity, IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers, ref GltfContainerComponent gltfContainer, PartitionComponent partitionComponent)
        {
            if (gltfContainer.GltfNodeEntities == null) return;

            // Check if transitioning from global modifier to individual modifiers
            if (gltfContainer.GltfNodeEntities.Count == 1 && gltfContainer.GltfNodeEntities[0] == containerEntity)
            {
                CleanupGltfNodeEntity(containerEntity, containerEntity);
                gltfContainer.GltfNodeEntities.Remove(containerEntity);
            }

            var existingGltfNodePaths = new Dictionary<string, Entity>();

            foreach (Entity nodeEntity in gltfContainer.GltfNodeEntities) { existingGltfNodePaths[World.Get<GltfNode>(nodeEntity).Path!] = nodeEntity; }

            foreach (PBGltfNodeModifiers.Types.GltfNodeModifier? modifier in modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

                if (existingGltfNodePaths.TryGetValue(modifier.Path, out Entity existingEntity))
                {
                    UpdateExistingGltfNodeEntity(existingEntity, modifier, hasShadowOverride, hasMaterialOverride, partitionComponent);
                    existingGltfNodePaths.Remove(modifier.Path); // Mark as processed
                }
                else { CreateNewGltfNodeEntity(containerEntity, modifier, ref gltfContainer, partitionComponent, hasShadowOverride, hasMaterialOverride); }
            }

            // Clean up entities that no longer have corresponding modifiers
            foreach (Entity orphanedEntity in existingGltfNodePaths.Values)
            {
                CleanupGltfNodeEntity(orphanedEntity, containerEntity);
                gltfContainer.GltfNodeEntities!.Remove(orphanedEntity);
            }
        }

        /// <summary>
        ///     Updates an existing GltfNode entity with new modifier data
        /// </summary>
        private void UpdateExistingGltfNodeEntity(Entity nodeEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, bool hasShadowOverride, bool hasMaterialOverride, PartitionComponent partitionComponent)
        {
            GltfNode gltfNode = World.Get<GltfNode>(nodeEntity);

            foreach (Renderer? renderer in gltfNode.Renderers)
                renderer.shadowCastingMode = !hasShadowOverride || modifier.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (hasMaterialOverride) { AddOrUpdateMaterial(nodeEntity, modifier.Material, partitionComponent); }
            else if (World.Has<PBMaterial>(nodeEntity))
            {
                // Material was removed, clean it up but don't destroy entity
                CreateMaterialCleanupIntention(nodeEntity, gltfNode.Renderers, gltfNode.ContainerEntity, false);
            }
        }
    }
}
