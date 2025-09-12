using Arch.Core;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.GltfNodeModifiers.Systems
{
    /// <summary>
    ///     Base class containing shared helper methods for GltfNodeModifier systems
    /// </summary>
    public abstract class GltfNodeModifierSystemBase : BaseUnityLoopSystem
    {
        protected GltfNodeModifierSystemBase(World world) : base(world) { }

        /// <summary>
        ///     Checks if the modifiers represent a global root modifier (single modifier with empty path)
        /// </summary>
        protected static bool IsGltfGlobalModifier(IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers) =>
            modifiers.Count == 1 && string.IsNullOrEmpty(modifiers[0].Path);

        /// <summary>
        ///     Gets the override flags for a modifier
        /// </summary>
        protected static (bool hasShadowOverride, bool hasMaterialOverride) GetModifierOverrides(PBGltfNodeModifiers.Types.GltfNodeModifier modifier)
        {
            bool hasShadowOverride = modifier.HasCastShadows;
            bool hasMaterialOverride = modifier.Material != null && modifier.Material.MaterialCase != PBMaterial.MaterialOneofCase.None;
            return (hasShadowOverride, hasMaterialOverride);
        }

        /// <summary>
        ///     Stores original materials for the specified renderers
        /// </summary>
        protected static void StoreOriginalMaterials(Dictionary<Renderer, Material> originalMaterials, IEnumerable<Renderer> renderers)
        {
            foreach (Renderer? renderer in renderers)
                originalMaterials[renderer] = renderer.sharedMaterial;
        }

        /// <summary>
        ///     Finds a renderer by path, returning null if not found
        /// </summary>
        protected static Renderer? FindRendererByPath(Transform gltfRootTransform, string path, IReadOnlyList<string>? availablePaths = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            // There's always 1 child GameObject in both AB or Raw GLTF instantiated GltfContainer...
            // AB: The GO name is "AB:hash"
            // Raw GLTF: the GO name is "Scene"
            Transform? rendererTransform = gltfRootTransform.GetChild(0).Find(path);

            if (rendererTransform != null && rendererTransform.TryGetComponent(out Renderer renderer))
                return renderer;

            ReportHub.LogError(ReportCategory.GLTF_CONTAINER,
                $"GLTF Node path '{path}' not found.");

            // Debug logging for local scene development when path is not found
            if (availablePaths is { Count: > 0 })
            {
                var pathsMessage = string.Join("\n  - ", availablePaths);
                ReportHub.LogError(ReportCategory.GLTF_CONTAINER,
                    $"GLTF Node available paths with renderers:\n  - {pathsMessage}");
            }

            return null;
        }

        /// <summary>
        ///     Resets shadow casting mode to default for all renderers in a GltfNode
        /// </summary>
        protected void ResetShadowCasting(Entity gltfNodeEntity)
        {
            GltfNode gltfNode = World.Get<GltfNode>(gltfNodeEntity);

            foreach (Renderer renderer in gltfNode.Renderers) { renderer.shadowCastingMode = ShadowCastingMode.On; }
        }

        /// <summary>
        ///     Cleans up a single GltfNode entity
        /// </summary>
        protected void CleanupGltfNodeEntity(Entity gltfNodeEntity, Entity containerEntity)
        {
            ResetShadowCasting(gltfNodeEntity);

            if (World.Has<PBMaterial>(gltfNodeEntity))
            {
                // Add cleanup intention for ResetMaterialSystem to handle
                ref GltfNode gltfNode = ref World.TryGetRef<GltfNode>(gltfNodeEntity, out bool exists);
                TriggerGltfNodeMaterialCleanup(gltfNodeEntity, ref gltfNode, true);
            }
            else
            {
                if (gltfNodeEntity != containerEntity) // Don't destroy the container entity itself
                    World.Destroy(gltfNodeEntity);
            }
        }

        /// <summary>
        ///     Cleans up all GLTF node entities and their associated data
        /// </summary>
        protected void CleanupAllGltfNodeEntities(Entity containerEntity, ref Components.GltfNodeModifiers nodeModifiers)
        {
            if (nodeModifiers.GltfNodeEntities.Count == 0) return;

            foreach (Entity entity in nodeModifiers.GltfNodeEntities.Keys)
                CleanupGltfNodeEntity(entity, containerEntity);

            nodeModifiers.GltfNodeEntities.Clear();
        }

        /// <summary>
        ///     Validates if the container is ready for processing modifiers
        /// </summary>
        protected bool IsGltfContainerReady(ref GltfContainerComponent gltfContainer, out StreamableLoadingResult<GltfContainerAsset> result)
        {
            result = default(StreamableLoadingResult<GltfContainerAsset>);

            if (gltfContainer.State != LoadingState.Finished) return false;

            result = gltfContainer.Promise.Result!.Value;

            return result.Succeeded;
        }

        /// <summary>
        ///     Creates a global GltfNode with all renderers
        /// </summary>
        protected void CreateGlobalGltfNode(Entity containerEntity, GltfContainerAsset asset)
        {
            World.Add(containerEntity, new GltfNode(asset.Renderers, containerEntity, string.Empty, false));
        }

        /// <summary>
        ///     Handles adding or updating a PBMaterial on an entity
        /// </summary>
        protected void AddOrUpdateMaterial(Entity entity, PBMaterial material, in PartitionComponent partitionComponent)
        {
            if (World.Has<PBMaterial>(entity))
            {
                material.IsDirty = true;
                World.Set(entity, material);
            }
            else
            {
                World.Add(entity, material, partitionComponent);
            }
        }

        /// <summary>
        ///     Creates the conditions for ResetMaterialSystem to reset the GltfNode entity
        /// </summary>
        protected void TriggerGltfNodeMaterialCleanup(Entity entity, ref GltfNode gltfNode, bool destroy)
        {
            gltfNode.CleanupDestruction = destroy;
            World.Remove<PBMaterial>(entity);
        }

        /// <summary>
        ///     Creates a new GLTF node entity for individual modifiers
        /// </summary>
        protected void CreateNewGltfNodeEntity(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, Transform gltfRoot, ref Components.GltfNodeModifiers nodeModifiers, PartitionComponent partitionComponent, bool hasShadowOverride, bool hasMaterialOverride, IReadOnlyList<string>? availablePaths = null)
        {
            Renderer? renderer = FindRendererByPath(gltfRoot, modifier.Path, availablePaths);
            if (renderer == null) return;

            Entity nodeEntity = this.World.Create();

            renderer.shadowCastingMode = !hasShadowOverride || modifier.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            World.Add(nodeEntity, new GltfNode(new[] { renderer }, containerEntity, modifier.Path));

            if (hasMaterialOverride)
                AddOrUpdateMaterial(nodeEntity, modifier.Material, partitionComponent);

            nodeModifiers.GltfNodeEntities!.Add(nodeEntity, modifier.Path);
        }
    }
}
