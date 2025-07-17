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
                var modifier = gltfNodeModifiers.Modifiers[0];

                // Handle global shadow override
                /*if (gltfNodeModifier == gltfNodeModifiers.Modifiers[0] && gltfNodeModifier.HasOverrideShadows)
                        result.Asset!.SetCastingShadows(gltfNodeModifier.OverrideShadows);*/

                // Skip if no material or invalid material
                if (modifier.Material != null && modifier.Material.MaterialCase != PBMaterial.MaterialOneofCase.None)
                {
                    // Store original materials for all renderers in the GLTF asset
                    foreach (var renderer in result.Asset!.Renderers)
                    {
                        gltfContainer.OriginalMaterials[renderer] = renderer.sharedMaterial;
                    }

                    // Add GltfNode to the container entity itself with all renderers
                    World.Add(entity, new GltfNode
                    {
                        Renderers = new List<Renderer>(result.Asset.Renderers),
                        ContainerEntity = entity
                    }, modifier.Material);

                    gltfContainer.GltfNodeEntities.Add(entity);
                }
            }
            else
            {
                // Normal case: create separate entities for each modifier
                foreach (var gltfNodeModifier in gltfNodeModifiers.Modifiers)
                {
                    // Handle global shadow override
                    /*if (gltfNodeModifier == gltfNodeModifiers.Modifiers[0] && gltfNodeModifier.HasOverrideShadows)
                            result.Asset!.SetCastingShadows(gltfNodeModifier.OverrideShadows);*/

                    // Skip if no material or invalid material
                    if (gltfNodeModifier.Material == null
                        || gltfNodeModifier.Material.MaterialCase == PBMaterial.MaterialOneofCase.None)
                        continue;

                    // Find the GameObject using the path
                    GameObject? targetGameObject = FindGameObjectByPath(gltfContainer.RootGameObject, gltfNodeModifier.Path);
                    if (targetGameObject == null)
                        continue;

                    // Get the Renderer component from the target GameObject
                    if (!targetGameObject.TryGetComponent(out Renderer renderer))
                        continue;

                    // Store original material only for the renderers used
                    gltfContainer.OriginalMaterials[renderer] = renderer.sharedMaterial;

                    Entity nodeEntity = this.World.Create();
                    World.Add(
                        nodeEntity,
                        new GltfNode { Renderers = new List<Renderer> { renderer }, ContainerEntity = entity },
                        gltfNodeModifier.Material,
                        partitionComponent);
                    gltfContainer.GltfNodeEntities.Add(nodeEntity);
                }
            }

            World.Add(entity, new GltfNodeModifiers());
        }

        [Query]
        [All(typeof(GltfNodeModifiers))]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleGltfNodeModifiersRemoval(Entity entity, in GltfContainerComponent gltfContainer)
        {
            if (gltfContainer.GltfNodeEntities == null || gltfContainer.GltfNodeEntities.Count == 0) return;

            foreach (Entity gltfNodeEntity in gltfContainer.GltfNodeEntities)
            {
                if(World.Has<PBMaterial>(gltfNodeEntity))
                    World.Remove<PBMaterial>(gltfNodeEntity); // ResetMaterialSystem takes care of the rest...
                else if (gltfNodeEntity != entity) // Don't destroy the container entity itself
                    World.Destroy(gltfNodeEntity);
            }
            gltfContainer.GltfNodeEntities.Clear();
            World.Remove<GltfNodeModifiers>(entity);
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
