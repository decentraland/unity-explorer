using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Audio;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using ECS.Abstract;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    /// <summary>
    ///     This system updates every cliff and water chunk renderers visibility  based on the camera position and distance settings
    /// </summary>
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeMiscCullingSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly TerrainGenerator terrainGenerator;
        private readonly List<MeshRenderer> cliffRenderers = new ();

        private bool isSetupDone;

        private NativeArray<float4> nativeFrustumPlanes;
        private NativeArray<VisibleBounds> cliffsBoundaries;
        private NativeArray<VisibleBounds> waterBoundaries;
        private Plane[] frustumPlanes;
        private JobHandle cliffsJobHandle;
        private JobHandle waterJobHandle;
        private bool cliffsUpdated;
        private bool waterUpdated;
        private List<MeshRenderer> waterRenderers;
        private Vector3 cameraPosition;

        private LandscapeMiscCullingSystem(World world,
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
            cliffsJobHandle = default(JobHandle);
            waterJobHandle = default(JobHandle);
        }

        public override void Dispose()
        {
            base.Dispose();
            cliffsJobHandle.Complete();
            waterJobHandle.Complete();
            nativeFrustumPlanes.Dispose();
            cliffsBoundaries.Dispose();
            waterBoundaries.Dispose();
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainGenerated()) return;

            if (!isSetupDone)
            {
                isSetupDone = true;
                InitializeMiscVisibility();
                InitializeWaterVisibility();
            }

            UpdateCliffsVisibility();
            UpdateWaterVisibility();

            if (cliffsJobHandle.IsCompleted && waterJobHandle.IsCompleted)
            {
                cliffsJobHandle.Complete();
                waterJobHandle.Complete();
                UpdateCameraFrustumPlanesQuery(World);
            }

            if (cliffsJobHandle.IsCompleted && cliffsUpdated)
            {
                Profiler.BeginSample("UpdateCliffsVisibility.Schedule");
                var job = new UpdateBoundariesCullingJob(cliffsBoundaries, nativeFrustumPlanes, cameraPosition, landscapeData.detailDistance);
                cliffsJobHandle = job.Schedule(cliffsBoundaries.Length, 32, cliffsJobHandle);
                cliffsUpdated = false;
                Profiler.EndSample();
            }

            if (waterJobHandle.IsCompleted && waterUpdated)
            {
                Profiler.BeginSample("UpdateWaterVisibility.Schedule");
                var job = new UpdateBoundariesCullingJob(waterBoundaries, nativeFrustumPlanes, cameraPosition, landscapeData.detailDistance);
                waterJobHandle = job.Schedule(waterBoundaries.Length, 32, waterJobHandle);
                waterUpdated = false;
                Profiler.EndSample();
            }
        }

        private void InitializeMiscVisibility()
        {
            IReadOnlyList<Transform> cliffs = terrainGenerator.GetCliffs();
            cliffsBoundaries = new NativeArray<VisibleBounds>(cliffs.Count * 3, Allocator.Persistent);

            for (var i = 0; i < cliffs.Count; i++)
            {
                Transform cliff = cliffs[i];
                MeshRenderer[] meshRenderers = cliff.GetComponentsInChildren<MeshRenderer>();

                for (var j = 0; j < meshRenderers.Length; j++)
                {
                    MeshRenderer cliffRenderer = meshRenderers[j];
                    Bounds bounds = cliffRenderer.bounds;

                    cliffsBoundaries[(i * 3) + j] = new VisibleBounds
                    {
                        Bounds = new AABB
                        {
                            Center = bounds.center,
                            Extents = bounds.extents + Vector3.one,
                        },
                    };

                    cliffRenderers.Add(cliffRenderer);
                }
            }
        }

        private void InitializeWaterVisibility()
        {
            Transform ocean = terrainGenerator.GetOcean();
            MeshRenderer[] renderers = ocean.GetComponentsInChildren<MeshRenderer>();

            // some water chunks are disabled on purpose, we dont want to re-enable them
            waterRenderers = renderers.Where(meshRenderer => meshRenderer.enabled).ToList();

            waterBoundaries = new NativeArray<VisibleBounds>(waterRenderers.Count, Allocator.Persistent);

            for (var i = 0; i < waterRenderers.Count; i++)
            {
                MeshRenderer waterChunk = waterRenderers[i];
                Bounds bounds = waterChunk.bounds;

                waterBoundaries[i] = new VisibleBounds
                {
                    Bounds = new AABB
                    {
                        Center = bounds.center,
                        Extents = bounds.extents + Vector3.one,
                    },
                };
            }
        }

        [Query]
        private void UpdateCameraFrustumPlanes(in Entity _, in CameraComponent cameraComponent)
        {
            Camera camera = cameraComponent.Camera;
            cameraPosition = camera.transform.position;

            var wind = terrainGenerator.GetWind();
            if(wind.parent == null)
                wind.parent = camera.transform;

            GeometryUtility.CalculateFrustumPlanes(camera.cullingMatrix, frustumPlanes);

            for (var i = 0; i < frustumPlanes.Length; i++)
            {
                Plane plane = frustumPlanes[i];
                nativeFrustumPlanes[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }
        }

        private void UpdateCliffsVisibility()
        {
            // Update Renderers
            if (cliffsJobHandle.IsCompleted)
            {
                Profiler.BeginSample("UpdateCliffsVisibility.Update");
                cliffsJobHandle.Complete();

                for (var i = 0; i < cliffRenderers.Count; i++)
                {
                    VisibleBounds visibility = cliffsBoundaries[i];

                    //if (visibility.IsHeard)
                    {
                        //WorldAudioEventsBus.Instance.SendPlayLandscapeAudioEvent(i, visibility.CalculatedVolume);
                    }
                    //else if (visibility.ShouldBeSilent && !visibility.IsSilent)
                    {
                      //  visibility.IsSilent = true;
                      //  WorldAudioEventsBus.Instance.SendStopLandscapeAudioEvent(i);
                    }


                    if (!visibility.IsDirty) continue;

                    MeshRenderer cliff = cliffRenderers[i];
                    cliff.forceRenderingOff = visibility is { IsVisible: false } or { IsAtDistance: false };
                }

                cliffsUpdated = true;
                Profiler.EndSample();
            }
        }

        private void UpdateWaterVisibility()
        {
            // Update Renderers
            if (waterJobHandle.IsCompleted)
            {
                Profiler.BeginSample("UpdateWaterVisibility.Update");
                waterJobHandle.Complete();

                for (var i = 0; i < waterRenderers.Count; i++)
                {
                    VisibleBounds visibility = waterBoundaries[i];

                    if (!visibility.IsDirty) continue;

                    MeshRenderer water = waterRenderers[i];
                    water.forceRenderingOff = visibility is { IsVisible: false } or { IsAtDistance: false };
                }

                waterUpdated = true;
                Profiler.EndSample();
            }
        }
    }
}
