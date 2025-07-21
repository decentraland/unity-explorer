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

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Handles initial setup of GLTF Node material modifiers
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class SetupGltfNodeModifierSystem : GltfNodeModifierSystemBase
    {
        public SetupGltfNodeModifierSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            SetupGltfNodesQuery(World);
        }

        [Query]
        [None(typeof(Components.GltfNodeModifiers))]
        private void SetupGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, in PartitionComponent partitionComponent)
        {
            if (gltfNodeModifiers.Modifiers.Count == 0 || !IsGltfContainerReady(ref gltfContainer, out StreamableLoadingResult<GltfContainerAsset> result))
                return;

            gltfNodeModifiers.IsDirty = false;
            gltfContainer.GltfNodeEntities ??= new List<Entity>();
            gltfContainer.OriginalMaterials ??= new Dictionary<Renderer, Material>();

            // Store original materials for all renderers (only happens once)
            if (gltfContainer.OriginalMaterials.Count == 0)
                StoreOriginalMaterials(ref gltfContainer, result.Asset!.Renderers);

            // Special case: single modifier with empty path applies to ALL renderers
            if (IsGltfRootModifier(gltfNodeModifiers.Modifiers))
                SetupGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref gltfContainer, result.Asset!);
            else
                SetupIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref gltfContainer, partitionComponent, result.Asset!);

            World.Add(entity, new Components.GltfNodeModifiers());
        }

        /// <summary>
        ///     Handles the special case where a single modifier with empty path applies to ALL renderers
        /// </summary>
        private void SetupGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref GltfContainerComponent gltfContainer, GltfContainerAsset asset)
        {
            (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

            // Add GltfNode to the container entity itself with all renderers
            CreateGlobalGltfNode(containerEntity, asset);

            asset.SetCastingShadows(!hasShadowOverride || modifier.CastShadows);

            if (hasMaterialOverride)
                World.Add(containerEntity, modifier.Material);

            gltfContainer.GltfNodeEntities!.Add(containerEntity);
        }

        /// <summary>
        ///     Handles the normal case where each modifier targets a specific renderer path
        /// </summary>
        private void SetupIndividualModifiers(Entity containerEntity, IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers, ref GltfContainerComponent gltfContainer, PartitionComponent partitionComponent, GltfContainerAsset asset)
        {
            foreach (PBGltfNodeModifiers.Types.GltfNodeModifier? modifier in modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                (bool hasShadowOverride, bool hasMaterialOverride) = GetModifierOverrides(modifier);

                CreateNewGltfNodeEntity(containerEntity, modifier, ref gltfContainer, partitionComponent, hasShadowOverride, hasMaterialOverride, asset.HierarchyPaths);
            }
        }
    }
}
