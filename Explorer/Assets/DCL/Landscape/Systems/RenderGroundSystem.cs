using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using ECS.Abstract;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Mathematics.Geometry;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(UpdateCinemachineBrainSystem))]
    public sealed partial class RenderGroundSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly TerrainGenerator terrainGenerator;
        private MaterialPropertyBlock materialProperties;

        private static readonly int PARCEL_SIZE_ID = Shader.PropertyToID("_ParcelSize");
        private static readonly int OCCUPANCY_MAP_ID = Shader.PropertyToID("_OccupancyMap");
        private static readonly int TERRAIN_BOUNDS_ID = Shader.PropertyToID("_TerrainBounds");

        private RenderGroundSystem(World world, LandscapeData landscapeData,
            TerrainGenerator terrainGenerator) : base(world)
        {
            this.landscapeData = landscapeData;
            this.terrainGenerator = terrainGenerator;
        }

        protected override void Update(float t)
        {
            if (!landscapeData.RenderGround || !terrainGenerator.IsTerrainShown)
                return;

            RenderGroundQuery(World);
        }

        [Query]
        private void RenderGround(ICinemachinePreset cinemachinePreset)
        {
            Camera camera = cinemachinePreset.Brain.OutputCamera;
            float3 cameraPosition = camera.transform.position;

            if (cameraPosition.y < 0f)
                return;

            float4x4 worldToClip = camera.projectionMatrix * camera.worldToCameraMatrix;
            var cameraFrustum = new ClipVolume(worldToClip, Allocator.TempJob);

            MinMaxAABB terrainBounds;
            terrainBounds.Min.x = terrainGenerator.TerrainModel.MinInUnits.x;
            terrainBounds.Min.y = -landscapeData.terrainData.minHeight;
            terrainBounds.Min.z = terrainGenerator.TerrainModel.MinInUnits.y;
            terrainBounds.Max.x = terrainGenerator.TerrainModel.MaxInUnits.x;
            terrainBounds.Max.y = terrainGenerator.MaxHeight - landscapeData.terrainData.minHeight;
            terrainBounds.Max.z = terrainGenerator.TerrainModel.MaxInUnits.y;

            if (!cameraFrustum.Overlaps(terrainBounds))
            {
                cameraFrustum.Dispose();
                return;
            }

            int parcelSize = landscapeData.terrainData.parcelSize;

            NativeArray<int> instanceCounts = new NativeArray<int>(
                landscapeData.GroundMeshes.Length, Allocator.TempJob);

            NativeList<Matrix4x4> transforms = new NativeList<Matrix4x4>(
                landscapeData.GroundInstanceCapacity, Allocator.TempJob);

            var generateGroundJob = new Jobs.GenerateGroundJob()
            {
                ParcelSize = parcelSize,
                TerrainBounds = terrainBounds,
                CameraPosition = cameraPosition,
                CameraFrustum = cameraFrustum,
                InstanceCounts = instanceCounts,
                Transforms = transforms
            };

            JobHandle generateGround = generateGroundJob.Schedule();

            if (materialProperties == null)
            {
                materialProperties = new MaterialPropertyBlock();
                materialProperties.SetTexture(OCCUPANCY_MAP_ID, terrainGenerator.OccupancyMap);
                materialProperties.SetFloat(PARCEL_SIZE_ID, parcelSize);

                materialProperties.SetVector(TERRAIN_BOUNDS_ID, new Vector4(
                    terrainBounds.Min.x, terrainBounds.Min.z, terrainBounds.Max.x,
                    terrainBounds.Max.z));
            }

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
    }
}
