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
            HandleGltfNodeModifiersCleanupQuery(World);

            SetupGltfNodesQuery(World);
            UpdateGltfNodesQuery(World);
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
            if (IsGltfRootModifier(gltfNodeModifiers.Modifiers))
                SetupGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref gltfContainer, result.Asset!);
            else
                SetupIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref gltfContainer, partitionComponent);

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
                ContainerEntity = containerEntity,
                Path = string.Empty
            });

            if (hasShadowOverride)
                asset.SetCastingShadows(modifier.OverrideShadows);

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
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                var (hasShadowOverride, hasMaterialOverride) = GetModifierOverrides(modifier);
                if (!hasShadowOverride && !hasMaterialOverride) continue;

                CreateNewGltfNodeEntity(containerEntity, modifier, ref gltfContainer, partitionComponent, hasShadowOverride, hasMaterialOverride);
            }
        }

        [Query]
        [All(typeof(GltfNodeModifiers))]
        private void UpdateGltfNodes(Entity entity, ref PBGltfNodeModifiers gltfNodeModifiers, ref GltfContainerComponent gltfContainer, in PartitionComponent partitionComponent)
        {
            if (!gltfNodeModifiers.IsDirty
                || gltfContainer.State != LoadingState.Finished
                || gltfContainer.RootGameObject == null
                || !gltfContainer.Promise.TryGetResult(World, out StreamableLoadingResult<GltfContainerAsset> result)
                || !result.Succeeded
                || gltfNodeModifiers.Modifiers.Count == 0)
                return;

            gltfNodeModifiers.IsDirty = false;

            // Special case: single modifier with empty path applies to ALL renderers
            if (IsGltfRootModifier(gltfNodeModifiers.Modifiers))
                UpdateGlobalModifier(entity, gltfNodeModifiers.Modifiers[0], ref gltfContainer, result.Asset!);
            else
                UpdateIndividualModifiers(entity, gltfNodeModifiers.Modifiers, ref gltfContainer, partitionComponent);
        }

        /// <summary>
        ///     Checks if the modifiers represent a global root modifier (single modifier with empty path)
        /// </summary>
        private static bool IsGltfRootModifier(IList<PBGltfNodeModifiers.Types.GltfNodeModifier> modifiers)
            => modifiers.Count == 1 && string.IsNullOrEmpty(modifiers[0].Path);

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

        /// <summary>
        ///     Updates the global modifier (single modifier with empty path)
        /// </summary>
        private void UpdateGlobalModifier(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref GltfContainerComponent gltfContainer, GltfContainerAsset asset)
        {
                        // Check if transitioning from individual modifiers to global modifier
            if (gltfContainer.GltfNodeEntities != null && gltfContainer.GltfNodeEntities.Count > 0 &&
                (gltfContainer.GltfNodeEntities.Count > 1 || gltfContainer.GltfNodeEntities[0] != containerEntity))
            {
                // Clean up individual modifier entities
                foreach (Entity entityToCleanup in gltfContainer.GltfNodeEntities)
                {
                    CleanupGltfNodeEntity(entityToCleanup, containerEntity, in gltfContainer);
                }
                gltfContainer.GltfNodeEntities.Clear();

                // Add global GltfNode component to container entity if transitioning from individual
                if (!World.Has<GltfNode>(containerEntity))
                {
                    World.Add(containerEntity, new GltfNode
                    {
                        Renderers = new List<Renderer>(asset.Renderers),
                        ContainerEntity = containerEntity,
                        Path = string.Empty
                    });
                }
                
                gltfContainer.GltfNodeEntities!.Add(containerEntity);
            }

            var (hasShadowOverride, hasMaterialOverride) = GetModifierOverrides(modifier);

            asset.SetCastingShadows(!hasShadowOverride || modifier.OverrideShadows);
            if (hasMaterialOverride)
            {
                if (World.Has<PBMaterial>(containerEntity))
                {
                    var updatedMaterial = modifier.Material;
                    updatedMaterial.IsDirty = true;
                    World.Set(containerEntity, updatedMaterial);
                }
                else
                {
                    StoreOriginalMaterials(ref gltfContainer, asset.Renderers);
                    World.Add(containerEntity, modifier.Material);
                }
            }
            else
            {
                if (World.Has<PBMaterial>(containerEntity))
                {
                    // Material was removed, clean it up but don't destroy entity
                    var gltfNode = World.Get<GltfNode>(containerEntity);
                    World.Add(containerEntity, new GltfNodeMaterialCleanupIntention
                    {
                        Renderers = gltfNode.Renderers,
                        ContainerEntity = gltfNode.ContainerEntity,
                        Destroy = false
                    });
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
                CleanupGltfNodeEntity(containerEntity, containerEntity, in gltfContainer);

            var existingGltfNodePaths = new Dictionary<string, Entity>();
            foreach (var nodeEntity in gltfContainer.GltfNodeEntities)
            {
                if (nodeEntity == containerEntity) continue;

                existingGltfNodePaths[World.Get<GltfNode>(nodeEntity).Path!] = nodeEntity;
            }

            foreach (var modifier in modifiers)
            {
                if (string.IsNullOrEmpty(modifier.Path)) continue; // Empty path only valid for global modifier

                var (hasShadowOverride, hasMaterialOverride) = GetModifierOverrides(modifier);
                if (!hasShadowOverride && !hasMaterialOverride) continue;

                if (existingGltfNodePaths.TryGetValue(modifier.Path, out var existingEntity))
                {
                    UpdateExistingGltfNodeEntity(existingEntity, modifier, hasMaterialOverride, partitionComponent);
                    existingGltfNodePaths.Remove(modifier.Path); // Mark as processed
                }
                else
                {
                    CreateNewGltfNodeEntity(containerEntity, modifier, ref gltfContainer, partitionComponent, hasShadowOverride, hasMaterialOverride);
                }
            }

            // Clean up entities that no longer have corresponding modifiers
            foreach (var orphanedEntity in existingGltfNodePaths.Values)
            {
                CleanupGltfNodeEntity(orphanedEntity, containerEntity, in gltfContainer);
                gltfContainer.GltfNodeEntities!.Remove(orphanedEntity);
            }
        }

        /// <summary>
        ///     Updates an existing GltfNode entity with new modifier data
        /// </summary>
        private void UpdateExistingGltfNodeEntity(Entity nodeEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, bool hasMaterialOverride, PartitionComponent partitionComponent)
        {
            var gltfNode = World.Get<GltfNode>(nodeEntity);
            foreach (var renderer in gltfNode.Renderers)
                renderer.shadowCastingMode = !modifier.HasOverrideShadows || modifier.OverrideShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (hasMaterialOverride)
            {
                if (World.Has<PBMaterial>(nodeEntity))
                {
                    var updatedMaterial = modifier.Material;
                    updatedMaterial.IsDirty = true;
                    World.Set(nodeEntity, updatedMaterial);
                }
                else
                {
                    World.Add(nodeEntity, modifier.Material, partitionComponent);
                }
            }
            else
            {
                if (World.Has<PBMaterial>(nodeEntity))
                {
                    // Material was removed, clean it up but don't destroy entity
                    World.Add(nodeEntity, new GltfNodeMaterialCleanupIntention
                    {
                        Renderers = gltfNode.Renderers,
                        ContainerEntity = gltfNode.ContainerEntity,
                        Destroy = false
                    });
                }
            }
        }

        /// <summary>
        ///     Creates a new GltfNode entity for a new modifier
        /// </summary>
        private void CreateNewGltfNodeEntity(Entity containerEntity, PBGltfNodeModifiers.Types.GltfNodeModifier modifier, ref GltfContainerComponent gltfContainer, PartitionComponent partitionComponent, bool hasShadowOverride, bool hasMaterialOverride)
        {
            var renderer = FindRendererByPath(gltfContainer.RootGameObject!, modifier.Path);
            if (renderer == null) return;

            Entity nodeEntity = this.World.Create();
            World.Add(nodeEntity, new GltfNode
            {
                Renderers = new List<Renderer> { renderer },
                ContainerEntity = containerEntity,
                Path = modifier.Path
            });

            if (hasShadowOverride)
                renderer.shadowCastingMode = modifier.OverrideShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            if (hasMaterialOverride)
            {
                StoreOriginalMaterials(ref gltfContainer, new[] { renderer });
                World.Add(nodeEntity, modifier.Material, partitionComponent);
            }

            gltfContainer.GltfNodeEntities!.Add(nodeEntity);
        }

        [Query]
        [All(typeof(GltfNodeModifiersCleanupIntention), typeof(GltfNodeModifiers))]
        private void HandleGltfNodeModifiersCleanup(Entity containerEntity, ref GltfContainerComponent gltfContainer)
        {
            if (gltfContainer.GltfNodeEntities == null || gltfContainer.GltfNodeEntities.Count == 0) return;

            foreach (Entity gltfNodeEntity in gltfContainer.GltfNodeEntities)
            {
                CleanupGltfNodeEntity(gltfNodeEntity, containerEntity, in gltfContainer);
            }

            gltfContainer.GltfNodeEntities.Clear();
            gltfContainer.GltfNodeEntities = null;
            World.Remove<GltfNodeModifiers>(containerEntity);
            World.Remove<GltfNodeModifiersCleanupIntention>(containerEntity);
        }

        [Query]
        [All(typeof(GltfNodeModifiers))]
        [None(typeof(PBGltfNodeModifiers))]
        private void HandleGltfNodeModifiersRemoval(Entity containerEntity, in GltfContainerComponent gltfContainer)
        {
            if (gltfContainer.GltfNodeEntities == null || gltfContainer.GltfNodeEntities.Count == 0) return;

            foreach (Entity gltfNodeEntity in gltfContainer.GltfNodeEntities)
            {
                CleanupGltfNodeEntity(gltfNodeEntity, containerEntity, in gltfContainer);
            }

            gltfContainer.GltfNodeEntities.Clear();
            World.Remove<GltfNodeModifiers>(containerEntity);
        }

        /// <summary>
        ///     Cleans up a single GltfNode entity during removal
        /// </summary>
        private void CleanupGltfNodeEntity(Entity gltfNodeEntity, Entity containerEntity, in GltfContainerComponent gltfContainer)
        {
            ResetShadowCasting(gltfNodeEntity);

            if (World.Has<PBMaterial>(gltfNodeEntity))
            {
                // Add cleanup intention for ResetMaterialSystem to handle
                var gltfNode = World.Get<GltfNode>(gltfNodeEntity);
                World.Add(gltfNodeEntity, new GltfNodeMaterialCleanupIntention
                {
                    Renderers = gltfNode.Renderers,
                    ContainerEntity = gltfNode.ContainerEntity,
                    Destroy = true
                });
                World.Remove<GltfNode>(gltfNodeEntity);
            }
            else
            {
                if (gltfNodeEntity != containerEntity) // Don't destroy the container entity itself
                    World.Destroy(gltfNodeEntity);
            }
        }

        /// <summary>
        ///     Resets shadow casting mode to default for all renderers in a GltfNode
        /// </summary>
        private void ResetShadowCasting(Entity gltfNodeEntity)
        {
            var gltfNode = World.Get<GltfNode>(gltfNodeEntity);
            foreach (Renderer renderer in gltfNode.Renderers)
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

            Transform? found = root.transform.Find(path);
            return found?.gameObject;
        }
    }
}
