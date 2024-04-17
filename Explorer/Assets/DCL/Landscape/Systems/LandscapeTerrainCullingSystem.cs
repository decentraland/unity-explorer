using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Vector3 = UnityEngine.Vector3;
using DCL.Audio;

namespace DCL.Landscape.Systems
{
    /// <summary>
    ///     This system updates every terrain visibility (terrain and trees) based on the camera position and distance settings
    ///     We schedule a parallel job to calculate the frustum and distance culling and then we use the job results to set-up the correct flags
    /// </summary>
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeTerrainCullingSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly TerrainGenerator terrainGenerator;
        private bool isTerrainViewInitialized;

        private NativeArray<float4> nativeFrustumPlanes;
        private NativeArray<VisibleBounds> terrainVisibilities;
        private NativeArray<NativeArray<int2>> terrainAudioSourcesPositions;
        private Plane[] frustumPlanes;
        private JobHandle jobHandle;

        private bool drawTerrain;
        private bool drawDetail;

        private LandscapeTerrainCullingSystem(World world,
            LandscapeData landscapeData,
            TerrainGenerator terrainGenerator) : base(world)
        {
            this.landscapeData = landscapeData;
            this.terrainGenerator = terrainGenerator;
        }

        public override void Initialize()
        {
            base.Initialize();

            frustumPlanes = new Plane[6];
            nativeFrustumPlanes = new NativeArray<float4>(6, Allocator.Persistent);
            jobHandle = default(JobHandle);

            drawTerrain = landscapeData.drawTerrain;
            drawDetail = landscapeData.drawTerrainDetails;
        }

        public override void Dispose()
        {
            base.Dispose();
            jobHandle.Complete();
            nativeFrustumPlanes.Dispose();
            terrainVisibilities.Dispose();
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainGenerated()) return;

            if (!isTerrainViewInitialized)
            {
                InitializeTerrainVisibility();
                isTerrainViewInitialized = true;
            }

            if (isTerrainViewInitialized)
                UpdateTerrainVisibilityQuery(World);
        }

        private void InitializeTerrainVisibility()
        {
            IReadOnlyList<Terrain> terrains = terrainGenerator.GetTerrains();
            terrainVisibilities = new NativeArray<VisibleBounds>(terrains.Count, Allocator.Persistent);
            terrainAudioSourcesPositions = new NativeArray<NativeArray<int2>>(terrains.Count, Allocator.Persistent);

            for (var i = 0; i < terrains.Count; i++)
            {
                Terrain terrain = terrains[i];
                Bounds bounds = GetTerrainBoundsInWorldSpace(terrain);
                ReportHub.Log(new ReportData(ReportCategory.LANDSCAPE), $"Adding Positions for terrain with Index {i} and bounds {bounds.min} - {bounds.max}");
                terrainAudioSourcesPositions[i] = GetAudioSourcesPositions(terrain.terrainData, bounds);

                terrainVisibilities[i] = new VisibleBounds
                {
                    Bounds = new AABB
                    {
                        Center = bounds.center,
                        Extents = bounds.extents,
                    },
                };
            }
        }

        private NativeArray<int2> GetAudioSourcesPositions(TerrainData terrainData, Bounds worldBounds)
        {
            Vector3 terrainSize = terrainData.size;
            int cellWidth = (int) terrainSize.x / 4;
            int cellLength = (int) terrainSize.z / 4;

            NativeList<int2> positions = new NativeList<int2>(Allocator.Temp);
            int2 worldCellCenter = new int2((int)worldBounds.center.x, (int)worldBounds.center.z);
            for (var row = 0; row < 4; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    int2 localCellCenter = new int2(
                        ((col * cellWidth) + (cellWidth / 2)),
                        ((row * cellLength) + (cellLength / 2))
                    );

                    //We could retry this 3 or 4 times until we find a valid point.
                    int2 randomOffset = new int2(UnityEngine.Random.Range(-cellWidth / 2, cellWidth / 2), UnityEngine.Random.Range(-cellLength / 2, cellLength / 2));
                    int2 randomPosition = localCellCenter + randomOffset;

                    if (!terrainData.IsHole(randomPosition.x, randomPosition.y))
                    {
                        positions.Add(worldCellCenter + randomPosition);
                        ReportHub.Log(new ReportData(ReportCategory.LANDSCAPE),$"Added position at {worldCellCenter + randomPosition}");
                    }
                }
            }

            return positions.ToArray(Allocator.Persistent);
        }

        [Query]
        private void UpdateTerrainVisibility(in Entity _, in CameraComponent cameraComponent)
        {
            // Update Renderers
            if (jobHandle.IsCompleted && !jobHandle.Equals(default(JobHandle)))
            {
                Profiler.BeginSample("UpdateTerrainVisibility.Update");
                jobHandle.Complete();

                bool isSettingsDirty = drawTerrain != landscapeData.drawTerrain || drawDetail != landscapeData.drawTerrainDetails;
                drawTerrain = landscapeData.drawTerrain;
                drawDetail = landscapeData.drawTerrainDetails;

                IReadOnlyList<Terrain> terrains = terrainGenerator.GetTerrains();

                for (var i = 0; i < terrainVisibilities.Length; i++)
                {
                    VisibleBounds visibility = terrainVisibilities[i];

                    if (visibility is {ShouldBeHeard: true, IsHeard: false })
                    {
                        //We put audiosources on the positions for this terrain
                        visibility.IsHeard = true;
                        terrainVisibilities[i] = visibility;
                        WorldAudioEventsBus.Instance.SendPlayLandscapeAudioEvent(i, terrainAudioSourcesPositions[i]);
                    }
                    else if (visibility is {ShouldBeSilent: true, IsSilent: false })
                    {
                        visibility.IsSilent = true;
                        terrainVisibilities[i] = visibility;
                        //We remove audiosources from this terrain
                        WorldAudioEventsBus.Instance.SendStopLandscapeAudioEvent(i);
                    }

                    if (!visibility.IsDirty && !isSettingsDirty) continue;

                    Terrain terrain = terrains[i];
                    terrain.drawHeightmap = visibility.IsVisible && landscapeData.drawTerrain;
                    terrain.drawTreesAndFoliage = visibility is { IsVisible: true, IsAtDistance: true } && landscapeData.drawTerrainDetails;
                }

                Profiler.EndSample();
            }

            // Schedule
            if (jobHandle.IsCompleted)
            {
                Profiler.BeginSample("UpdateTerrainVisibility.Schedule");
                jobHandle.Complete();

                Camera camera = cameraComponent.Camera;
                Vector3 cameraPosition = camera.transform.position;

                GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, frustumPlanes);

                for (var i = 0; i < frustumPlanes.Length; i++)
                {
                    Plane plane = frustumPlanes[i];
                    nativeFrustumPlanes[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
                }

                var job = new UpdateBoundariesCullingJob(terrainVisibilities, nativeFrustumPlanes, cameraPosition, landscapeData.detailDistance);
                jobHandle = job.Schedule(terrainVisibilities.Length, 32, jobHandle);
                Profiler.EndSample();
            }

        }

        private Bounds GetTerrainBoundsInWorldSpace(Terrain terrain)
        {
            Bounds localBounds = terrain.terrainData.bounds;
            Vector3 terrainPosition = terrain.transform.position;
            var worldBounds = new Bounds(localBounds.center + terrainPosition, localBounds.size);
            return worldBounds;
        }
    }
}
