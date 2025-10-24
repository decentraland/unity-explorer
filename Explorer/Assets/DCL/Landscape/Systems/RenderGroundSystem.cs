using Arch.Core;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using Decentraland.Terrain;
using ECS.Abstract;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using static Unity.Mathematics.math;
using ClipVolume = DCL.Landscape.Utils.ClipVolume;

namespace DCL.Landscape.Systems
{
    /// <summary>Renders ground and grass, but not trees. Trees are drawn using GPU Instancer Pro. See
    /// <see cref="TreeData"/> for more.</summary>
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(UpdateCinemachineBrainSystem))]
    public sealed partial class RenderGroundSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly global::Global.Dynamic.Landscapes.Landscape landscape;
        private readonly MaterialPropertyBlock materialProperties;
        private readonly GrassIndirectRenderer? grassIndirectRenderer;

        private static readonly int PARCEL_SIZE_ID = Shader.PropertyToID("_ParcelSize");
        private static readonly int MIN_DIST_OCCUPANCY_ID = Shader.PropertyToID("_MinDistOccupancy");
        private static readonly int OCCUPANCY_MAP_ID = Shader.PropertyToID("_OccupancyMap");
        private static readonly int TERRAIN_BOUNDS_ID = Shader.PropertyToID("_TerrainBounds");
        private static readonly int DISTANCE_FIELD_SCALE_ID = Shader.PropertyToID("_DistanceFieldScale");

        private RenderGroundSystem(World world, global::Global.Dynamic.Landscapes.Landscape landscape, LandscapeData landscapeData)
            : base(world)
        {
            this.landscapeData = landscapeData;
            this.landscape = landscape;
            landscape.TerrainLoaded += OnTerrainLoaded;
            grassIndirectRenderer = landscapeData.GrassIndirectRenderer;

            materialProperties = new MaterialPropertyBlock();
            materialProperties.SetFloat(PARCEL_SIZE_ID, landscapeData.terrainData.parcelSize);

            if (grassIndirectRenderer != null)
                landscape.TerrainLoaded += grassIndirectRenderer.OnTerrainLoaded;
        }

        protected override void Update(float t)
        {
            ITerrain terrain = landscape.CurrentTerrain;

            if (!terrain.IsTerrainShown)
                return;

            SingleInstanceEntity cameraEntity = World.CacheCamera();

            if (World.TryGet(cameraEntity, out ICinemachinePreset? cinemachinePreset))
            {
                Camera camera = cinemachinePreset!.Brain.OutputCamera;

                RenderGroundInternal(camera);

#if UNITY_EDITOR
                const bool RENDER_TO_ALL_CAMERAS = true;
#else
                const bool RENDER_TO_ALL_CAMERAS = false;
#endif

                if (grassIndirectRenderer != null)
                    grassIndirectRenderer.Render(landscapeData, terrain, camera, RENDER_TO_ALL_CAMERAS);
            }
        }

        private void RenderGroundInternal(Camera camera)
        {
            float3 cameraPosition = camera.transform.position;

            if (cameraPosition.y < 0f)
                return;

            float4x4 worldToClip = camera.projectionMatrix * camera.worldToCameraMatrix;
            var cameraFrustum = new ClipVolume(worldToClip, Allocator.TempJob);
            MinMaxAABB terrainBounds = GetTerrainBounds();

            if (!cameraFrustum.Overlaps(terrainBounds))
            {
                cameraFrustum.Dispose();
                return;
            }

            NativeArray<int> instanceCounts = new NativeArray<int>(
                landscapeData.GroundMeshes.Length, Allocator.TempJob);

            NativeList<Matrix4x4> transforms = new NativeList<Matrix4x4>(
                landscapeData.GroundInstanceCapacity, Allocator.TempJob);

            var generateGroundJob = new GenerateGroundJob
            {
                ParcelSize = landscapeData.terrainData.parcelSize,
                TerrainBounds = terrainBounds,
                CameraPosition = cameraPosition,
                CameraFrustum = cameraFrustum,
                InstanceCounts = instanceCounts,
                Transforms = transforms
            };

            JobHandle generateGround = generateGroundJob.Schedule();

            var renderParams = new RenderParams()
            {
                layer = 1, // Default
                material = landscapeData.GroundMaterial,
                matProps = materialProperties,
                receiveShadows = true,
                renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                worldBounds = terrainBounds.ToBounds()
            };

#if !UNITY_EDITOR
            renderParams.camera = camera;
#endif

            generateGround.Complete();
            cameraFrustum.Dispose();

            if (transforms.Length == 0)
                return;

            if (transforms.Length > landscapeData.GroundInstanceCapacity)
            {
                landscapeData.GroundInstanceCapacity
                    = (int)ceil(landscapeData.GroundInstanceCapacity * 1.1f);

                ReportHub.LogWarning(ReportCategory.LANDSCAPE,
                    $"The {nameof(transforms)} list ran out of space. Increasing capacity to {landscapeData.GroundInstanceCapacity}.");
            }

            int startInstance = 0;

            for (int meshIndex = 0; meshIndex < landscapeData.GroundMeshes.Length; meshIndex++)
            {
                int instanceCount = instanceCounts[meshIndex];

                if (instanceCount == 0)
                    continue;

                Graphics.RenderMeshInstanced(renderParams, landscapeData.GroundMeshes[meshIndex], 0,
                    transforms.AsArray(), instanceCount, startInstance);

                startInstance += instanceCount;
            }

            instanceCounts.Dispose();
            transforms.Dispose();
        }

        private MinMaxAABB GetTerrainBounds()
        {
            ITerrain terrain = landscape.CurrentTerrain;
            TerrainModel terrainModel = terrain.TerrainModel!;
            MinMaxAABB terrainBounds;
            terrainBounds.Min.x = terrainModel.MinInUnits.x;
            terrainBounds.Min.y = -landscapeData.terrainData.minHeight;
            terrainBounds.Min.z = terrainModel.MinInUnits.y;
            terrainBounds.Max.x = terrainModel.MaxInUnits.x;
            terrainBounds.Max.y = terrain.MaxHeight - landscapeData.terrainData.minHeight;
            terrainBounds.Max.z = terrainModel.MaxInUnits.y;
            return terrainBounds;
        }

        private void OnTerrainLoaded(ITerrain terrain)
        {
            materialProperties.SetFloat(DISTANCE_FIELD_SCALE_ID, terrain.MaxHeight);
            materialProperties.SetFloat(MIN_DIST_OCCUPANCY_ID, terrain.OccupancyFloor / 255f);

            materialProperties.SetTexture(OCCUPANCY_MAP_ID,
                terrain.OccupancyMap != null ? terrain.OccupancyMap : Texture2D.blackTexture);

            MinMaxAABB terrainBounds = GetTerrainBounds();

            materialProperties.SetVector(TERRAIN_BOUNDS_ID, new Vector4(
                terrainBounds.Min.x, terrainBounds.Min.z, terrainBounds.Max.x, terrainBounds.Max.z));
        }
    }
}
