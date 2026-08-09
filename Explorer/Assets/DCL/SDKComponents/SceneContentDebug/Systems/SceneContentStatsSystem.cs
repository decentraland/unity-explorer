using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.ECSComponents;
using DCL.Profiling;
using DCL.SDKComponents.MediaStream;
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
using UnityEngine.Rendering;

namespace DCL.SDKComponents.SceneContentDebug.Systems
{
    /// <summary>
    ///     Counts the scene's content (entities, triangles, meshes, geometries, materials, textures,
    ///     shader variants, colliders and videos) into <see cref="SceneContentStats" />.
    ///     Collection is throttled and runs only while <see cref="SceneContentStats.CollectionRequested" />
    ///     is set, so the system is idle unless a consumer (the "Scene content" debug widget, the
    ///     scene debug menu metrics panel or the MCP get_scene_content_stats tool) requests the stats.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class SceneContentStatsSystem : BaseUnityLoopSystem
    {
        private const int COLLECTION_COOLDOWN_FRAMES = 60;

        private static readonly int[] TEXTURE_PROPERTY_IDS =
        {
            Shader.PropertyToID("_BaseMap"),
            Shader.PropertyToID("_MainTex"),
            Shader.PropertyToID("_BumpMap"),
            Shader.PropertyToID("_EmissionMap"),
            Shader.PropertyToID("_MetallicGlossMap"),
            Shader.PropertyToID("_OcclusionMap"),
        };

        private static readonly QueryDescription MEDIA_PLAYERS_QUERY = new QueryDescription()
                                                                     .WithAll<MediaPlayerComponent>()
                                                                     .WithNone<DeleteEntityIntention>();

        private readonly SceneContentStats stats;
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;

        private const string PRIMITIVES_SOURCE = "(primitive meshes)";

        private readonly HashSet<Mesh> uniqueMeshes = new ();
        private readonly HashSet<Material> uniqueMaterials = new ();
        private readonly HashSet<Texture> uniqueTextures = new ();
        private readonly HashSet<long> uniqueShaderVariants = new ();
        private readonly HashSet<long> sourceVariantsScratch = new ();
        private readonly Dictionary<Shader, LocalKeyword[]> shaderKeywordsCache = new ();
        private readonly List<Material> materialsScratch = new ();
        private readonly Dictionary<string, SceneContentBreakdownEntry> breakdownScratch = new ();
        private readonly Dictionary<string, HashSet<Material>> breakdownMaterialsScratch = new ();

        private int framesSinceCollection = COLLECTION_COOLDOWN_FRAMES;

        private bool collectBreakdown;

        private long triangles;
        private int bodies;
        private int geometries;
        private int materials;
        private int textures;
        private int shaderVariants;
        private int colliders;
        private int videos;

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
                    uniqueShaderVariants.Clear();
                    shaderKeywordsCache.Clear();
                    breakdownScratch.Clear();
                    breakdownMaterialsScratch.Clear();
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
            shaderVariants = 0;
            colliders = 0;

            uniqueMeshes.Clear();
            uniqueMaterials.Clear();
            uniqueTextures.Clear();
            uniqueShaderVariants.Clear();

            collectBreakdown = stats.BreakdownRequests > 0;

            if (collectBreakdown)
            {
                breakdownScratch.Clear();
                breakdownMaterialsScratch.Clear();
            }

            CountPrimitiveMeshesQuery(World);
            CountGltfContainersQuery(World);
            CountSdkMaterialsQuery(World);
            CountPrimitiveCollidersQuery(World);
            videos = World.CountEntities(in MEDIA_PLAYERS_QUERY);

            if (collectBreakdown)
                FlushBreakdown();

            stats.Entities = entitiesMap.Count;
            stats.Triangles = triangles;
            stats.Bodies = bodies;
            stats.Geometries = geometries;
            stats.Materials = materials;
            stats.Textures = textures;
            stats.ShaderVariants = shaderVariants;
            stats.Colliders = colliders;
            stats.Videos = videos;
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
            int materialSlots = AccountRendererMaterials(component.MeshRenderer, collectBreakdown ? GetBreakdownMaterials(PRIMITIVES_SOURCE) : null);

            if (collectBreakdown)
            {
                bool visible = component.MeshRenderer.isVisible;

                AccountBreakdown(PRIMITIVES_SOURCE, meshTriangles, rendererCount: 1, materialSlots,
                    visible ? meshTriangles : 0, visible ? 1 : 0, visible ? materialSlots : 0);
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
            HashSet<Material>? sourceMaterials = collectBreakdown ? GetBreakdownMaterials(sdkComponent.Src) : null;
            long containerTriangles = 0;
            var containerMaterialSlots = 0;
            long visibleTriangles = 0;
            var visibleRenderers = 0;
            var visibleMaterialSlots = 0;

            for (var i = 0; i < renderers.Count; i++)
            {
                Renderer renderer = renderers[i];

                Mesh? mesh = renderer switch
                             {
                                 SkinnedMeshRenderer skinned => skinned.sharedMesh,
                                 _ => renderer.TryGetComponent(out MeshFilter meshFilter) ? meshFilter.sharedMesh : null,
                             };

                bodies++;

                long rendererTriangles = mesh != null ? AccountMesh(mesh) : 0;
                containerTriangles += rendererTriangles;

                int materialSlots = AccountRendererMaterials(renderer, sourceMaterials);
                containerMaterialSlots += materialSlots;

                if (collectBreakdown && renderer.isVisible)
                {
                    visibleRenderers++;
                    visibleTriangles += rendererTriangles;
                    visibleMaterialSlots += materialSlots;
                }
            }

            if (collectBreakdown)
                AccountBreakdown(sdkComponent.Src, containerTriangles, renderers.Count, containerMaterialSlots,
                    visibleTriangles, visibleRenderers, visibleMaterialSlots);
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

        private void AccountBreakdown(string source, long containerTriangles, int rendererCount, int materialSlots,
            long visibleTriangles, int visibleRenderers, int visibleMaterialSlots)
        {
            breakdownScratch.TryGetValue(source, out SceneContentBreakdownEntry entry);
            entry.Source = source;
            entry.Instances++;
            entry.Renderers += rendererCount;
            entry.Triangles += containerTriangles;
            entry.DrawCalls += materialSlots;
            entry.VisibleRenderers += visibleRenderers;
            entry.VisibleTriangles += visibleTriangles;
            entry.VisibleDrawCalls += visibleMaterialSlots;
            breakdownScratch[source] = entry;
        }

        // Allocates one set per source; only reached during explicitly requested breakdown passes
        private HashSet<Material> GetBreakdownMaterials(string source)
        {
            if (!breakdownMaterialsScratch.TryGetValue(source, out HashSet<Material>? materials))
            {
                materials = new HashSet<Material>();
                breakdownMaterialsScratch[source] = materials;
            }

            return materials;
        }

        private void FlushBreakdown()
        {
            stats.BreakdownEntries.Clear();

            foreach (KeyValuePair<string, SceneContentBreakdownEntry> pair in breakdownScratch)
            {
                SceneContentBreakdownEntry entry = pair.Value;

                if (breakdownMaterialsScratch.TryGetValue(pair.Key, out HashSet<Material>? sourceMaterials))
                {
                    entry.Materials = sourceMaterials.Count;
                    entry.ShaderVariants = CountShaderVariants(sourceMaterials);
                }

                stats.BreakdownEntries.Add(entry);
            }
        }

        private int CountShaderVariants(HashSet<Material> sourceMaterials)
        {
            sourceVariantsScratch.Clear();

            foreach (Material material in sourceMaterials)
                sourceVariantsScratch.Add(ComputeVariantKey(material));

            return sourceVariantsScratch.Count;
        }

        private int AccountRendererMaterials(Renderer renderer, HashSet<Material>? breakdownMaterials = null)
        {
            renderer.GetSharedMaterials(materialsScratch);

            for (var i = 0; i < materialsScratch.Count; i++)
            {
                Material material = materialsScratch[i];

                if (material != null)
                {
                    AccountMaterial(material);
                    breakdownMaterials?.Add(material);
                }
            }

            return materialsScratch.Count;
        }

        private void AccountMaterial(Material material)
        {
            if (!uniqueMaterials.Add(material)) return;

            materials++;

            if (uniqueShaderVariants.Add(ComputeVariantKey(material)))
                shaderVariants++;

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

        /// <summary>
        ///     Identity of the material's shader variant — the shader plus its enabled local keywords,
        ///     the bin the SRP Batcher batches draws by. Keyword hashes are XOR-combined so the key is
        ///     independent of enumeration order; cross-set collisions are negligible at counting precision.
        /// </summary>
        private long ComputeVariantKey(Material material)
        {
            Shader shader = material.shader;

            if (!shaderKeywordsCache.TryGetValue(shader, out LocalKeyword[]? keywords))
            {
                // Allocates once per distinct shader, not per material
                keywords = shader.keywordSpace.keywords;
                shaderKeywordsCache[shader] = keywords;
            }

            long key = (int)shader.GetEntityId();

            for (var i = 0; i < keywords.Length; i++)
                if (material.IsKeywordEnabled(keywords[i]))
                    key ^= (long)keywords[i].name.GetHashCode() << 17;

            return key;
        }
    }
}
