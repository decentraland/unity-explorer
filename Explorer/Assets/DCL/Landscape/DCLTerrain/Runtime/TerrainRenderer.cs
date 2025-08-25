using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using static Decentraland.Terrain.TerrainLog;
using static Unity.Mathematics.math;
using Object = UnityEngine.Object;

namespace Decentraland.Terrain
{
    [ExecuteAlways]
    public sealed class TerrainRenderer : MonoBehaviour
    {
        [field: SerializeField] private TerrainData TerrainData { get; set; }
        [field: SerializeField] internal GrassIndirectRenderer DetailIndirectRenderer { get; private set; }

        private TerrainRendererState state;

        private static MaterialPropertyBlock groundMaterialProperties;
        private static MaterialPropertyBlock treeMaterialProperties;

        private static readonly int INV_PARCEL_SIZE_ID = Shader.PropertyToID("_InvParcelSize");
        private static readonly int OCCUPANCY_MAP_ID = Shader.PropertyToID("_OccupancyMap");
        private static readonly int TERRAIN_BOUNDS_ID = Shader.PropertyToID("_TerrainBounds");

#if UNITY_EDITOR
        internal int DetailInstanceCount { get; private set; }
        internal int GroundInstanceCount { get; private set; }
        internal int TreeInstanceCount { get; private set; }
#endif

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TerrainData == null)
            {
                LogHandler.LogFormat(LogType.Error, this, "Terrain data is not set up properly");
                enabled = false;
                return;
            }
#endif

            state ??= Application.isPlaying
                ? new TerrainRendererState(TerrainData)
                : new TerrainRendererState();

            state.RenderDetailIndirect = DetailIndirectRenderer != null;
            state.Renderer = this;
            state.TerrainData = TerrainData;

#if UNITY_EDITOR
            state.RenderToAllCameras = true;
#else
            state.RenderToAllCameras = false;
#endif

            Camera camera;

#if UNITY_EDITOR
            if (SceneVisibilityManager.instance.IsHidden(gameObject))
                return;

            if (!Application.isPlaying)
            {
                SceneView sceneView = SceneView.lastActiveSceneView;
                camera = sceneView != null ? sceneView.camera : Camera.main;
            }
            else
#endif
            {
                camera = Camera.main;
            }

            if (camera == null)
                return;

            if (TerrainData.RenderDetail && state.RenderDetailIndirect)
                DetailIndirectRenderer.Render(TerrainData, camera, state.RenderToAllCameras);

            Render(state, camera);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Bounds bounds = Bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }

        public Bounds Bounds => TerrainData != null ? GetBounds(TerrainData) : default(Bounds);

        private static Bounds GetBounds(TerrainData terrainData)
        {
            RectInt bounds = terrainData.Bounds;
            int parcelSize = terrainData.ParcelSize;
            float maxHeight = terrainData.MaxHeight;
            Vector2 center = bounds.center * parcelSize;
            Vector2Int size = bounds.size * parcelSize;

            return new Bounds(new Vector3(center.x, maxHeight * 0.5f, center.y),
                new Vector3(size.x, maxHeight, size.y));
        }

        public static void Render(TerrainRendererState state, Camera camera)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state.TerrainData == null)
                throw new ArgumentNullException(nameof(state));

            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
#endif

            bool renderGround = state.TerrainData.RenderGround
                                && state.TerrainData.GroundMaterial != null
                                && state.TerrainData.GroundMeshes.Length == 3;

            bool renderTrees = state.TerrainData.RenderTrees
                               && state.TerrainData.TreePrototypes.Length > 0;

            bool renderDetail = state.TerrainData.RenderDetail
                                && state.TerrainData.DetailPrototypes.Length > 0
                                && !state.RenderDetailIndirect;

            if (!renderGround && !renderTrees && !renderDetail)
                return;

            float3 cameraPosition = camera.transform.position;

            if (cameraPosition.y < 0f)
                return;

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;

            var projMatrix = Matrix4x4.Perspective(camera.fieldOfView, camera.aspect,
                camera.nearClipPlane, Mathf.Min(camera.farClipPlane, state.TerrainData.DetailDistance));

            Matrix4x4 worldToClip = projMatrix * viewMatrix;
            var cameraFrustum = new ClipVolume(worldToClip, Allocator.TempJob);

            Bounds bounds = GetBounds(state.TerrainData);

            if (!cameraFrustum.Overlaps(bounds.ToMinMaxAABB()))
            {
                cameraFrustum.Dispose();
                return;
            }

            Profiler.BeginSample("RenderTerrain");

            TerrainDataData terrainData = state.TerrainData.GetData();

            JobHandle generateGround;
            NativeArray<int> groundInstanceCounts;
            NativeList<Matrix4x4> groundTransforms;

            if (renderGround)
            {
                groundInstanceCounts = new NativeArray<int>(state.TerrainData.GroundMeshes.Length,
                    Allocator.TempJob);

                groundTransforms = new NativeList<Matrix4x4>(state.TerrainData.GroundInstanceCapacity,
                    Allocator.TempJob);

                var generateGroundJob = new GenerateGroundJob
                {
                    TerrainData = terrainData,
                    CameraPosition = cameraPosition,
                    CameraFrustum = cameraFrustum,
                    InstanceCounts = groundInstanceCounts,
                    Transforms = groundTransforms,
                };

                generateGround = generateGroundJob.Schedule();
            }
            else
            {
                generateGround = default(JobHandle);
                groundInstanceCounts = default(NativeArray<int>);
                groundTransforms = default(NativeList<Matrix4x4>);
            }

            var scatterRectMin = (int2)floor(cameraFrustum.Bounds.Min.xz / terrainData.ParcelSize);

            int2 scatterRectSize = (int2)ceil(cameraFrustum.Bounds.Max.xz / terrainData.ParcelSize)
                                   - scatterRectMin;

            var scatterRect = new RectInt(scatterRectMin.x, scatterRectMin.y, scatterRectSize.x,
                scatterRectSize.y);

            scatterRect.ClampToBounds(state.TerrainData.Bounds);

            JobHandle scatterTrees;
            NativeList<TreeInstanceData> treeInstances;
            NativeArray<int> treeInstanceCounts;
            JobHandle prepareTreeRenderList;
            NativeList<Matrix4x4> treeTransforms;

            if (renderTrees)
            {
                if (state.SpeedTreeParent != null)
                    state.SpeedTreeParent.position = inverse(worldToClip)
                       .MultiplyPoint(float3(0f, 0f, EPSILON));

                var treeMeshCount = 0;

                for (var prototypeIndex = 0;
                     prototypeIndex < state.TerrainData.TreePrototypes.Length;
                     prototypeIndex++) { treeMeshCount += state.TerrainData.TreePrototypes[prototypeIndex].Lods.Length; }

                // Deallocated by ScatterTreesJob
                var treeLods = new NativeArray<TreeLODData>(treeMeshCount, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                for (var prototypeIndex = 0;
                     prototypeIndex < state.TerrainData.TreePrototypes.Length;
                     prototypeIndex++)
                {
                    TreeLOD[] lods = state.TerrainData.TreePrototypes[prototypeIndex].Lods;
                    int lod0MeshIndex = terrainData.TreePrototypes[prototypeIndex].Lod0MeshIndex;

                    for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                        treeLods[lod0MeshIndex + lodIndex] = new TreeLODData(lods[lodIndex]);
                }

                treeInstances = new NativeList<TreeInstanceData>(state.TerrainData.TreeInstanceCapacity,
                    Allocator.TempJob);

                var scatterTreesJob = new ScatterTreesJob
                {
                    TerrainData = terrainData,
                    CameraPosition = cameraPosition,
                    CameraFrustum = cameraFrustum,
                    RectMin = int2(scatterRect.x, scatterRect.y),
                    RectSizeX = scatterRect.width,
                    Lods = treeLods,
                    Instances = treeInstances.AsParallelWriter(),
                };

                scatterTrees = scatterTreesJob.Schedule(scatterRect.width * scatterRect.height);

                treeInstanceCounts = new NativeArray<int>(treeMeshCount, Allocator.TempJob);
                treeTransforms = new NativeList<Matrix4x4>(Allocator.TempJob);

                var prepareTreeRenderListJob = new PrepareTreeRenderListJob
                {
                    instances = treeInstances,
                    instanceCounts = treeInstanceCounts,
                    transforms = treeTransforms,
                };

                prepareTreeRenderList = prepareTreeRenderListJob.Schedule(scatterTrees);
            }
            else
            {
                scatterTrees = default(JobHandle);
                treeInstances = default(NativeList<TreeInstanceData>);
                treeInstanceCounts = default(NativeArray<int>);
                prepareTreeRenderList = default(JobHandle);
                treeTransforms = default(NativeList<Matrix4x4>);
            }

            JobHandle scatterDetail;
            NativeList<DetailInstanceData> detailInstances;
            NativeArray<int> detailInstanceCounts;
            JobHandle prepareDetailRenderList;
            NativeList<Matrix4x4> detailTransforms;

            if (renderDetail)
            {
                // Deallocated by ScatterDetailJob
                var detailPrototypes = new NativeArray<DetailPrototypeData>(
                    state.TerrainData.DetailPrototypes.Length, Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                for (var i = 0; i < detailPrototypes.Length; i++)
                    detailPrototypes[i] = new DetailPrototypeData(
                        state.TerrainData.DetailPrototypes[i]);

                detailInstances = new NativeList<DetailInstanceData>(
                    state.TerrainData.DetailInstanceCapacity, Allocator.TempJob);

                var scatterDetailJob = new ScatterDetailJob
                {
                    TerrainData = terrainData,
                    CameraPosition = cameraPosition,
                    CameraFrustum = cameraFrustum,
                    RectMin = int2(scatterRect.x, scatterRect.y),
                    RectSizeX = scatterRect.width,
                    Instances = detailInstances.AsParallelWriter(),
                };

                scatterDetail = scatterDetailJob.Schedule(scatterRect.width * scatterRect.height);

                detailInstanceCounts = new NativeArray<int>(detailPrototypes.Length, Allocator.TempJob);
                detailTransforms = new NativeList<Matrix4x4>(Allocator.TempJob);

                var prepareDetailRenderListJob = new PrepareDetailRenderListJob
                {
                    instances = detailInstances,
                    instanceCounts = detailInstanceCounts,
                    transforms = detailTransforms,
                };

                prepareDetailRenderList = prepareDetailRenderListJob.Schedule(scatterDetail);
            }
            else
            {
                scatterDetail = default(JobHandle);
                detailInstances = default(NativeList<DetailInstanceData>);
                detailInstanceCounts = default(NativeArray<int>);
                prepareDetailRenderList = default(JobHandle);
                detailTransforms = default(NativeList<Matrix4x4>);
            }

            var renderParams = new RenderParams
            {
                layer = 1, // Default
                receiveShadows = true,
                renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                worldBounds = bounds,
            };

            if (!state.RenderToAllCameras)
                renderParams.camera = camera;

            if (renderGround)
            {
                generateGround.Complete();

                if (groundTransforms.Length > state.TerrainData.GroundInstanceCapacity)
                {
                    state.TerrainData.GroundInstanceCapacity
                        = (int)ceil(state.TerrainData.GroundInstanceCapacity * 1.1f);

                    LogHandler.LogFormat(LogType.Warning, state.TerrainData,
                        "The {0} list ran out of space. Increasing capacity to {1}.",
                        nameof(groundTransforms), state.TerrainData.GroundInstanceCapacity);
                }

#if UNITY_EDITOR
                if (state.Renderer != null)
                    state.Renderer.GroundInstanceCount = groundTransforms.Length;
#endif

                RenderGround(state.TerrainData, renderParams, groundTransforms.AsArray(),
                    groundInstanceCounts);

                groundInstanceCounts.Dispose();
                groundTransforms.Dispose();
            }

            if (renderTrees)
            {
                scatterTrees.Complete();
                prepareTreeRenderList.Complete();

                if (treeInstances.Length > treeInstances.Capacity)
                {
                    state.TerrainData.TreeInstanceCapacity
                        = (int)ceil(state.TerrainData.TreeInstanceCapacity * 1.1f);

                    LogHandler.LogFormat(LogType.Warning, state.TerrainData,
                        "The {0} list ran out of space. Increasing capacity to {1}.",
                        nameof(treeInstances), state.TerrainData.TreeInstanceCapacity);
                }

#if UNITY_EDITOR
                if (state.Renderer != null)
                    state.Renderer.TreeInstanceCount = treeInstances.Length;
#endif

                treeInstances.Dispose();

                RenderTrees(state, renderParams, treeTransforms.AsArray(), treeInstanceCounts);

                treeInstanceCounts.Dispose();
                treeTransforms.Dispose();
            }

            if (renderDetail)
            {
                scatterDetail.Complete();
                prepareDetailRenderList.Complete();

                if (detailInstances.Length > detailInstances.Capacity)
                {
                    state.TerrainData.DetailInstanceCapacity
                        = (int)ceil(state.TerrainData.DetailInstanceCapacity * 1.1f);

                    LogHandler.LogFormat(LogType.Warning, state.TerrainData,
                        "The {0} list ran out of space. Increasing capacity to {1}.",
                        nameof(detailInstances), state.TerrainData.DetailInstanceCapacity);
                }

#if UNITY_EDITOR
                if (state.Renderer != null)
                    state.Renderer.DetailInstanceCount = detailInstances.Length;
#endif

                detailInstances.Dispose();

                RenderDetail(state, renderParams, detailTransforms.AsArray(),
                    detailInstanceCounts);

                detailInstanceCounts.Dispose();
                detailTransforms.Dispose();
            }

            cameraFrustum.Dispose();
            Profiler.EndSample();
        }

        private static void RenderDetail(TerrainRendererState state, RenderParams renderParams,
            NativeArray<Matrix4x4> instanceData, NativeArray<int> instanceCounts)
        {
            if (instanceData.Length == 0)
                return;

            DetailPrototype[] prototypes = state.TerrainData.DetailPrototypes;
            var startInstance = 0;

            for (var prototypeIndex = 0; prototypeIndex < prototypes.Length; prototypeIndex++)
            {
                // Since details do not have LODs, prototypeIndex is the same as meshIndex.
                int instanceCount = instanceCounts[prototypeIndex];

                if (instanceCount == 0)
                    continue;

                DetailPrototype prototype = prototypes[prototypeIndex];
                renderParams.material = prototype.Material;

                Graphics.RenderMeshInstanced(renderParams, prototype.Mesh, 0, instanceData,
                    instanceCount, startInstance);

                startInstance += instanceCount;
            }
        }

        private static void RenderGround(TerrainData terrainData, RenderParams renderParams,
            NativeArray<Matrix4x4> instanceData, NativeArray<int> instanceCounts)
        {
            if (instanceData.Length == 0)
                return;

            var bounds = new Vector4(
                terrainData.Bounds.x, terrainData.Bounds.x + terrainData.Bounds.width,
                terrainData.Bounds.y, terrainData.Bounds.y + terrainData.Bounds.height);

            if (groundMaterialProperties == null)
                groundMaterialProperties = new MaterialPropertyBlock();

            if (terrainData.OccupancyMap != null)
                groundMaterialProperties.SetTexture(OCCUPANCY_MAP_ID, terrainData.OccupancyMap);
            else
                groundMaterialProperties.Clear();

            groundMaterialProperties.SetFloat(INV_PARCEL_SIZE_ID, 1f / terrainData.ParcelSize);
            groundMaterialProperties.SetVector(TERRAIN_BOUNDS_ID, bounds * terrainData.ParcelSize);

            var startInstance = 0;
            renderParams.material = terrainData.GroundMaterial;
            renderParams.matProps = groundMaterialProperties;
            renderParams.shadowCastingMode = ShadowCastingMode.On;

            for (var meshIndex = 0; meshIndex < terrainData.GroundMeshes.Length; meshIndex++)
            {
                int instanceCount = instanceCounts[meshIndex];

                if (instanceCount == 0)
                    continue;

                Graphics.RenderMeshInstanced(renderParams, terrainData.GroundMeshes[meshIndex], 0,
                    instanceData, instanceCount, startInstance);

                startInstance += instanceCount;
            }
        }

        private static void RenderTrees(TerrainRendererState state, RenderParams renderParams,
            NativeArray<Matrix4x4> instanceData, NativeArray<int> instanceCounts)
        {
            if (instanceData.Length == 0)
                return;

            if (treeMaterialProperties == null)
                treeMaterialProperties = new MaterialPropertyBlock();

            TreePrototype[] prototypes = state.TerrainData.TreePrototypes;
            var meshIndex = 0;
            var startInstance = 0;
            renderParams.shadowCastingMode = ShadowCastingMode.On;

            for (var prototypeIndex = 0; prototypeIndex < prototypes.Length; prototypeIndex++)
            {
                if (state.SpeedTreeRenderers != null
                    && prototypeIndex < state.SpeedTreeRenderers.Length)
                {
                    Renderer renderer = state.SpeedTreeRenderers[prototypeIndex];

                    if (renderer != null)
                    {
                        renderer.GetPropertyBlock(treeMaterialProperties);
                        renderParams.matProps = treeMaterialProperties;
                    }
                    else { renderParams.matProps = null; }
                }

                TreeLOD[] lods = prototypes[prototypeIndex].Lods;

                for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    // When you concatenate the LOD lists of all the prototypes, you get the list
                    // meshIndex indexes into.
                    int instanceCount = instanceCounts[meshIndex];
                    meshIndex++;

                    if (instanceCount == 0)
                        continue;

                    TreeLOD lod = lods[lodIndex];

                    for (var subMeshIndex = 0; subMeshIndex < lod.Materials.Length; subMeshIndex++)
                    {
                        renderParams.material = lod.Materials[subMeshIndex];

                        Graphics.RenderMeshInstanced(renderParams, lod.Mesh, subMeshIndex, instanceData,
                            instanceCount, startInstance);
                    }

                    startInstance += instanceCount;
                }
            }
        }

        [BurstCompile]
        private struct PrepareDetailRenderListJob : IJob
        {
            public NativeList<DetailInstanceData> instances;
            [WriteOnly] public NativeArray<int> instanceCounts;
            public NativeList<Matrix4x4> transforms;

            public void Execute()
            {
                // If NativeList<T>.ParallelWriter runs out of space, the length of the list will exceed
                // its capacity. This code deals with that.
                int totalInstanceCount = min(instances.Length, instances.Capacity);

                if (totalInstanceCount == 0)
                    return;

                instances.AsArray().GetSubArray(0, totalInstanceCount).Sort();
                transforms.Capacity = totalInstanceCount;

                var instanceCount = 0;
                var meshIndex = 0;

                for (var instanceIndex = 0; instanceIndex < totalInstanceCount; instanceIndex++)
                {
                    DetailInstanceData instance = instances[instanceIndex];

                    if (meshIndex < instance.MeshIndex)
                    {
                        instanceCounts[meshIndex] = instanceCount;
                        meshIndex = instance.MeshIndex;
                        instanceCount = 0;
                    }

                    instanceCount++;

                    transforms.AddNoResize(Matrix4x4.TRS(instance.Position,
                        Quaternion.Euler(0f, instance.RotationY, 0f),
                        new Vector3(instance.ScaleXZ, instance.ScaleY, instance.ScaleXZ)));
                }

                instanceCounts[meshIndex] = instanceCount;
            }
        }

        [BurstCompile]
        private struct PrepareTreeRenderListJob : IJob
        {
            public NativeList<TreeInstanceData> instances;
            [WriteOnly] public NativeArray<int> instanceCounts;
            public NativeList<Matrix4x4> transforms;

            public void Execute()
            {
                // If NativeList<T>.ParallelWriter runs out of space, the length of the list will exceed
                // its capacity. This code deals with that.
                int totalInstanceCount = min(instances.Length, instances.Capacity);

                if (totalInstanceCount == 0)
                    return;

                instances.AsArray().GetSubArray(0, totalInstanceCount).Sort();
                transforms.Capacity = totalInstanceCount;

                var instanceCount = 0;
                var meshIndex = 0;

                for (var instanceIndex = 0; instanceIndex < totalInstanceCount; instanceIndex++)
                {
                    TreeInstanceData instance = instances[instanceIndex];

                    if (meshIndex < instance.MeshIndex)
                    {
                        instanceCounts[meshIndex] = instanceCount;
                        meshIndex = instance.MeshIndex;
                        instanceCount = 0;
                    }

                    instanceCount++;

                    transforms.AddNoResize(Matrix4x4.TRS(instance.Position,
                        Quaternion.Euler(0f, instance.RotationY, 0f),
                        Vector3.one));
                }

                instanceCounts[meshIndex] = instanceCount;
            }
        }
    }

    public sealed class TerrainRendererState : IDisposable
    {
        public bool RenderDetailIndirect;
        internal TerrainRenderer Renderer;
        public bool RenderToAllCameras;
        internal Transform SpeedTreeParent;
        internal Renderer[] SpeedTreeRenderers;
        public TerrainData TerrainData;

        public TerrainRendererState() { }

        public TerrainRendererState(TerrainData terrainData)
        {
            TerrainData = terrainData;

#if TERRAIN
            var windAssets = new SpeedTreeWindAsset[TerrainData.TreePrototypes.Length];
            var haveWindAssets = false;

            for (var i = 0; i < TerrainData.TreePrototypes.Length; i++)
            {
                TreePrototype prototype = terrainData.TreePrototypes[i];
                Tree tree = prototype.Source.GetComponentInChildren<Tree>();

                if (tree == null || tree.windAsset == null)
                    continue;

                windAssets[i] = tree.windAsset;
                haveWindAssets = true;
            }

            if (!haveWindAssets)
                return;

            SpeedTreeParent = new GameObject("SpeedTreeWind").transform;
            SpeedTreeRenderers = new Renderer[terrainData.TreePrototypes.Length];

            for (var i = 0; i < terrainData.TreePrototypes.Length; i++)
            {
                SpeedTreeWindAsset windAsset = windAssets[i];

                if (windAsset == null)
                    continue;

                TreePrototype prototype = terrainData.TreePrototypes[i];
                var gameObject = new GameObject(prototype.Source.name);
                SpeedTreeRenderers[i] = gameObject.AddComponent<MeshRenderer>();
                Tree tree = gameObject.AddComponent<Tree>();
                tree.windAsset = windAssets[i];
                gameObject.transform.SetParent(SpeedTreeParent, false);
            }
#endif
        }

        public void Dispose()
        {
            if (SpeedTreeParent != null)
                Object.Destroy(SpeedTreeParent.gameObject);
        }
    }
}
