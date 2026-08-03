using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.ECSComponents;
using DCL.Profiling;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.SceneContentDebug.Systems
{
    /// <summary>
    ///     Counts the scene's content (entities, triangles, meshes, geometries, materials, textures,
    ///     colliders and external content) into <see cref="SceneContentStats" />.
    ///     Collection is throttled and runs only while <see cref="SceneContentStats.CollectionRequested" />
    ///     is set, so the system is idle unless a consumer (the "Current scene" debug widget, the
    ///     scene debug menu metrics panel or the MCP get_scene_content_stats tool) requests the stats.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class SceneContentStatsSystem : BaseUnityLoopSystem
    {
        private const int COLLECTION_COOLDOWN_FRAMES = 30;

        private static readonly int[] TEXTURE_PROPERTY_IDS =
        {
            Shader.PropertyToID("_BaseMap"),
            Shader.PropertyToID("_MainTex"),
            Shader.PropertyToID("_BumpMap"),
            Shader.PropertyToID("_EmissionMap"),
            Shader.PropertyToID("_MetallicGlossMap"),
            Shader.PropertyToID("_OcclusionMap"),
        };

        private static readonly QueryDescription NFT_SHAPES_QUERY = new QueryDescription()
                                                                   .WithAll<NftShapeRendererComponent>()
                                                                   .WithNone<DeleteEntityIntention>();

        private readonly SceneContentStats stats;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;

        private readonly HashSet<Mesh> uniqueMeshes = new ();
        private readonly HashSet<Material> uniqueMaterials = new ();
        private readonly HashSet<Texture> uniqueTextures = new ();
        private readonly List<Material> materialsScratch = new ();
        private readonly Dictionary<string, SceneContentBreakdownEntry> breakdownScratch = new ();

        private int framesSinceCollection = COLLECTION_COOLDOWN_FRAMES;

        private bool collectBreakdown;
        private int primitiveInstances;
        private long primitiveTriangles;

        private long triangles;
        private int bodies;
        private int geometries;
        private int materials;
        private int textures;
        private int colliders;
        private int externalContent;

        internal SceneContentStatsSystem(World world, SceneRuntimeMetrics runtimeMetrics, Dictionary<CRDTEntity, Entity> entitiesMap) : base(world)
        {
            stats = runtimeMetrics.ContentStats;
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            if (!stats.CollectionRequested)
            {
                // Keep the counter primed so reopening the widget collects on the very next frame,
                // and drop asset references held from the last pass
                framesSinceCollection = COLLECTION_COOLDOWN_FRAMES;

                if (uniqueMeshes.Count > 0)
                {
                    uniqueMeshes.Clear();
                    uniqueMaterials.Clear();
                    uniqueTextures.Clear();
                }

                return;
            }

            if (++framesSinceCollection < COLLECTION_COOLDOWN_FRAMES) return;
            framesSinceCollection = 0;

            Collect();
        }

        private void Collect()
        {
            triangles = 0;
            bodies = 0;
            geometries = 0;
            materials = 0;
            textures = 0;
            colliders = 0;
            externalContent = 0;

            uniqueMeshes.Clear();
            uniqueMaterials.Clear();
            uniqueTextures.Clear();

            collectBreakdown = stats.BreakdownRequested;

            if (collectBreakdown)
            {
                breakdownScratch.Clear();
                primitiveInstances = 0;
                primitiveTriangles = 0;
            }

            CountPrimitiveMeshesQuery(World);
            CountGltfContainersQuery(World);
            CountSdkMaterialsQuery(World);
            CountPrimitiveCollidersQuery(World);
            CountMediaStreamsQuery(World);
            externalContent += World.CountEntities(in NFT_SHAPES_QUERY);

            if (collectBreakdown)
                FlushBreakdown();

            stats.Entities = entitiesMap.Count;
            stats.Triangles = triangles;
            stats.Bodies = bodies;
            stats.Geometries = geometries;
            stats.Materials = materials;
            stats.Textures = textures;
            stats.Colliders = colliders;
            stats.ExternalContent = externalContent;
            stats.HasData = true;
            stats.CollectionCount++;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountPrimitiveMeshes(in PrimitiveMeshRendererComponent component)
        {
            Mesh? mesh = component.PrimitiveMesh?.Mesh;
            if (mesh == null) return;

            bodies++;
            long meshTriangles = AccountMesh(mesh);
            AccountRendererMaterials(component.MeshRenderer);

            if (collectBreakdown)
            {
                primitiveInstances++;
                primitiveTriangles += meshTriangles;
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountGltfContainers(in GltfContainerComponent component, in PBGltfContainer sdkComponent)
        {
            if (component.State != LoadingState.Finished) return;

            StreamableLoadingResult<GltfContainerAsset>? result = component.Promise.Result;
            if (result is not { Succeeded: true }) return;

            GltfContainerAsset asset = result.Value.Asset!;

            colliders += asset.InvisibleColliders.Count + (asset.DecodedVisibleSDKColliders?.Count ?? 0);

            List<Renderer> renderers = asset.Renderers;
            long containerTriangles = 0;

            for (var i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];

                Mesh? mesh = renderer switch
                             {
                                 SkinnedMeshRenderer skinned => skinned.sharedMesh,
                                 _ => renderer.TryGetComponent(out MeshFilter meshFilter) ? meshFilter.sharedMesh : null,
                             };

                bodies++;

                if (mesh != null)
                    containerTriangles += AccountMesh(mesh);

                AccountRendererMaterials(renderer);
            }

            if (collectBreakdown)
                AccountBreakdown(sdkComponent.Src, containerTriangles, renderers.Count);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountSdkMaterials(in MaterialComponent component)
        {
            if (component.Result != null)
                AccountMaterial(component.Result);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountPrimitiveColliders(in PrimitiveColliderComponent component)
        {
            if (component.Collider != null)
                colliders++;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void CountMediaStreams(in MediaPlayerComponent component)
        {
            if (!component.IsFromContentServer)
                externalContent++;
        }

        private long AccountMesh(Mesh mesh)
        {
            long meshTriangles = 0;

            for (var i = 0; i < mesh.subMeshCount; i++)
                meshTriangles += mesh.GetIndexCount(i) / 3;

            triangles += meshTriangles;

            if (uniqueMeshes.Add(mesh))
                geometries++;

            return meshTriangles;
        }

        private void AccountBreakdown(string source, long containerTriangles, int rendererCount)
        {
            breakdownScratch.TryGetValue(source, out SceneContentBreakdownEntry entry);
            entry.Source = source;
            entry.Instances++;
            entry.Renderers += rendererCount;
            entry.Triangles += containerTriangles;
            breakdownScratch[source] = entry;
        }

        private void FlushBreakdown()
        {
            stats.BreakdownEntries.Clear();

            foreach (KeyValuePair<string, SceneContentBreakdownEntry> pair in breakdownScratch)
                stats.BreakdownEntries.Add(pair.Value);

            if (primitiveInstances > 0)
                stats.BreakdownEntries.Add(new SceneContentBreakdownEntry
                {
                    Source = "(primitive meshes)",
                    Instances = primitiveInstances,
                    Renderers = primitiveInstances,
                    Triangles = primitiveTriangles,
                });

            stats.BreakdownRequested = false;
        }

        private void AccountRendererMaterials(Renderer renderer)
        {
            renderer.GetSharedMaterials(materialsScratch);

            for (var i = 0; i < materialsScratch.Count; i++)
            {
                Material material = materialsScratch[i];

                if (material != null)
                    AccountMaterial(material);
            }
        }

        private void AccountMaterial(Material material)
        {
            if (!uniqueMaterials.Add(material)) return;

            materials++;

            for (var i = 0; i < TEXTURE_PROPERTY_IDS.Length; i++)
            {
                int propertyId = TEXTURE_PROPERTY_IDS[i];
                if (!material.HasProperty(propertyId)) continue;

                Texture? texture = material.GetTexture(propertyId);
                if (texture == null) continue;

                if (uniqueTextures.Add(texture))
                    textures++;
            }
        }
    }
}
