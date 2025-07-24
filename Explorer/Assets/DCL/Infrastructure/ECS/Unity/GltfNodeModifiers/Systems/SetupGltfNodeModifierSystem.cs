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
using ECS.Unity.GLTFContainer.Systems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Handles initial setup of GLTF Node material modifiers
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    [UpdateAfter(typeof(FinalizeGltfContainerLoadingSystem))]
    public partial class SetupGltfNodeModifierSystem : GltfNodeModifierSystemBase
    {
        public SetupGltfNodeModifierSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SetupGltfNodesQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(Components.GltfNodeModifiers))]
        private void SetupGltfNodes(Entity entity, PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, PartitionComponent partitionComponent)
        {
            if (gltfNodeModifiers.Modifiers.Count == 0 || !IsGltfContainerReady(ref gltfContainer, out StreamableLoadingResult<GltfContainerAsset> result))
                return;

            gltfNodeModifiers.IsDirty = false;
            var nodeModifiers = new Components.GltfNodeModifiers(DictionaryPool<Entity, string>.Get(), DictionaryPool<Renderer, Material>.Get());

            StoreOriginalMaterials(nodeModifiers.OriginalMaterials, result.Asset!.Renderers);

            // Special case: single modifier with empty path applies to ALL renderers
            if (IsGltfGlobalModifier(gltfNodeModifiers.Modifiers))
                SetupGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref nodeModifiers, result.Asset, partitionComponent);
            else
                SetupIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref nodeModifiers, partitionComponent, result.Asset);

            World.Add(entity, nodeModifiers);
        }

        /// <summary>
        ///     Sets up the global modifier (single modifier with empty path)
        /// </summary>
        private void SetupGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref Components.GltfNodeModifiers nodeModifiers, GltfContainerAsset asset, PartitionComponent partitionComponent)
        {
            (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

            asset.SetCastingShadows(!hasShadowOverride || modifier.CastShadows);

            CreateGlobalGltfNode(containerEntity, asset);

            if (hasMaterialOverride)
                AddOrUpdateMaterial(containerEntity, modifier.Material, partitionComponent);

            nodeModifiers.GltfNodeEntities.Add(containerEntity, string.Empty);
        }

        /// <summary>
        ///     Sets up individual modifiers (multiple modifiers or specific paths)
        /// </summary>
        private void SetupIndividualModifiers(Entity containerEntity, IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers, ref Components.GltfNodeModifiers nodeModifiers, PartitionComponent partitionComponent, GltfContainerAsset asset)
        {
            foreach (PBGltfNodeModifiers.Types.GltfNodeModifier modifier in modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

                CreateNewGltfNodeEntity(containerEntity, modifier, asset.Root.transform, ref nodeModifiers, partitionComponent, hasShadowOverride, hasMaterialOverride, asset.HierarchyPaths);
            }
        }
    }
}
