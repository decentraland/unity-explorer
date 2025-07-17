using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Materials.Components;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     Handles GLTF Node material modifiers by assigning and updating PBMaterial components
    /// </summary>
    [UpdateInGroup(typeof(GltfContainerGroup))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class GltfNodeModifierSystem : BaseUnityLoopSystem
    {
        internal GltfNodeModifierSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            HandleGltfNodeModifiersRemovalQuery(World);

            SetupGltfNodesQuery(World);
            // UpdateGltfNodesQuery(World);
        }

        [Query]
        [None(typeof(GltfNodeModifiers))]
        private void SetupGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, in PartitionComponent partitionComponent)
        {
            if (gltfNodeModifiers.Modifiers.Count == 0
                || gltfContainer.State != LoadingState.Finished
                || gltfContainer.RootGameObject == null
                || !gltfContainer.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result)
                || !result.Succeeded)
                return;

            gltfNodeModifiers.IsDirty = false;
            gltfContainer.GltfNodeEntities ??= new List<Entity>();
            gltfContainer.OriginalMaterials ??= new Dictionary<Renderer, Material>();

            // Special case: single modifier with empty path applies to ALL renderers
            if (gltfNodeModifiers.Modifiers.Count == 1 && string.IsNullOrEmpty(gltfNodeModifiers.Modifiers[0].Path))
            {
                SetupGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref gltfContainer, result.Asset!);
            }
            else
            {
                SetupIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref gltfContainer, partitionComponent);
            }

            World.Add(entity, new GltfNodeModifiers());
        }

        /// <summary>
        ///     Handles the special case where a single modifier with empty path applies to ALL renderers
        /// </summary>
        private void SetupGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref GltfContainerComponent gltfContainer, GltfContainerAsset asset)
        {
            var (hasShadowOverride, hasMaterialOverride) = GetModifierOverrides(modifier);
            if (!hasShadowOverride && !hasMaterialOverride) return;

            // Add GltfNode to the container entity itself with all renderers
            World.Add(containerEntity, new GltfNode
            {
                Renderers = new List<Renderer>(asset.Renderers),
                ContainerEntity = containerEntity
            });

            // Handle global shadow override
            if (hasShadowOverride)
                asset.SetCastingShadows(modifier.OverrideShadows);

            // Handle material override
            if (hasMaterialOverride)
            {
                StoreOriginalMaterials(ref gltfContainer, asset.Renderers);
                World.Add(containerEntity, modifier.Material);
            }

            gltfContainer.GltfNodeEntities!.Add(containerEntity);
        }

        /// <summary>
        ///     Handles the normal case where each modifier targets a specific renderer path
        /// </summary>
        private void SetupIndividualModifiers(Entity containerEntity, IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers, ref GltfContainerComponent gltfContainer, PartitionComponent partitionComponent)
        {
            foreach (var modifier in modifiers)
            {
                var (hasShadowOverride, hasMaterialOverride) = GetModifierOverrides(modifier);
                if (!hasShadowOverride && !hasMaterialOverride) continue;

                // Find the Renderer using the path
                var renderer = FindRendererByPath(gltfContainer.RootGameObject!, modifier.Path);
                if (renderer == null) continue;

                Entity nodeEntity = this.World.Create();
                World.Add(nodeEntity, new GltfNode { Renderers = new List<Renderer> { renderer }, ContainerEntity = containerEntity });

                // Handle individual shadow override
                if (hasShadowOverride)
                    renderer.shadowCastingMode = modifier.OverrideShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

                // Handle material override
                if (hasMaterialOverride)
                {
                    StoreOriginalMaterials(ref gltfContainer, new[] { renderer });
                    World.Add(nodeEntity, modifier.Material, partitionComponent);
                }

                gltfContainer.GltfNodeEntities!.Add(nodeEntity);
            }
        }

        /// <summary>
        ///     Gets the override flags for a modifier
        /// </summary>
        private static (bool hasShadowOverride, bool hasMaterialOverride) GetModifierOverrides(PBGltfNodeModifiers.Types.GltfNodeModifier modifier)
        {
            var hasShadowOverride = modifier.HasOverrideShadows;
            var hasMaterialOverride = modifier.Material != null && modifier.Material.MaterialCase != PBMaterial.MaterialOneofCase.None;
            return (hasShadowOverride, hasMaterialOverride);
        }

        /// <summary>
        ///     Stores original materials for the specified renderers
        /// </summary>
        private static void StoreOriginalMaterials(ref GltfContainerComponent gltfContainer, IEnumerable<Renderer> renderers)
        {
            foreach (var renderer in renderers)
                gltfContainer.OriginalMaterials![renderer] = renderer.sharedMaterial;
        }

        /// <summary>
        ///     Finds a renderer by path, returning null if not found
        /// </summary>
        private static Renderer? FindRendererByPath(GameObject rootGameObject, string path)
        {
            var targetGameObject = FindGameObjectByPath(rootGameObject, path);
            return targetGameObject != null && targetGameObject.TryGetComponent(out Renderer renderer) ? renderer : null;
        }

        // TODO: HANDLE GLTFCONTAINER REMOVAL ???
        [Query]
        [All(typeof(GltfNodeModifiers))]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleGltfNodeModifiersRemoval(Entity containerEntity, in GltfContainerComponent gltfContainer)
        {
            if (gltfContainer.GltfNodeEntities == null || gltfContainer.GltfNodeEntities.Count == 0) return;

            foreach (Entity gltfNodeEntity in gltfContainer.GltfNodeEntities)
            {
                CleanupGltfNodeEntity(gltfNodeEntity, containerEntity);
            }

            gltfContainer.GltfNodeEntities.Clear();
            World.Remove<GltfNodeModifiers>(containerEntity);
        }

        /// <summary>
        ///     Cleans up a single GltfNode entity during removal
        /// </summary>
        private void CleanupGltfNodeEntity(Entity gltfNodeEntity, Entity containerEntity)
        {
            if (World.Has<PBMaterial>(gltfNodeEntity))
            {
                World.Remove<PBMaterial>(gltfNodeEntity); // ResetMaterialSystem takes care of the rest...
            }
            else
            {
                ResetShadowCasting(gltfNodeEntity);

                if (gltfNodeEntity != containerEntity) // Don't destroy the container entity itself
                    World.Destroy(gltfNodeEntity);
            }
        }

        /// <summary>
        ///     Resets shadow casting mode to default for all renderers in a GltfNode
        /// </summary>
        private void ResetShadowCasting(Entity gltfNodeEntity)
        {
            foreach (Renderer renderer in World.Get<GltfNode>(gltfNodeEntity).Renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        /// <summary>
        ///     Finds a GameObject using Unity's built-in path navigation with Transform.Find
        /// </summary>
        private static GameObject? FindGameObjectByPath(GameObject root, string path)
        {
            if (string.IsNullOrEmpty(path))
                return root;

            // Unity's Transform.Find already supports path navigation with '/' separators
            Transform? found = root.transform.Find(path);
            return found?.gameObject;
        }

        /*        // [Query]
        // [All(typeof(MaterialComponent), typeof(PBMaterial), typeof(GltfNodeModifiers))]
        // private void UpdateGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer)
        // {
        //     if (!gltfNodeModifiers.IsDirty
        //         || gltfContainer.State != LoadingState.Finished
        //         || !gltfContainer.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result)
        //         || !result.Succeeded
        //         || gltfNodeModifiers.Modifiers.Count == 0)
        //         return;
        //     gltfNodeModifiers.IsDirty = false;
        //     var rootGltfNodeModifier = gltfNodeModifiers.Modifiers[0];
        //
        //     result.Asset!.SetCastingShadows(!rootGltfNodeModifier.HasOverrideShadows || rootGltfNodeModifier.OverrideShadows);
        //
        //     if (rootGltfNodeModifier.Material == null
        //         || rootGltfNodeModifier.Material.MaterialCase == PBMaterial.MaterialOneofCase.None)
        //     {
        //         gltfContainer.ResetOriginalMaterials();
        //         return;
        //     }
        //
        //     var pbMaterial = rootGltfNodeModifier.Material;
        //     pbMaterial.IsDirty = true;
        //     World.Set(entity, pbMaterial);
        // }*/
    }
}
