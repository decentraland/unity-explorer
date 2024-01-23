using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Components;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using DCL.MapRenderer.ComponentsFactory;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using Utility;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeParcelInitializerSystem : BaseUnityLoopSystem
    {
        private const int MAX_JOB_CONCURRENCY = 600;
        private const int MAX_OWNED_PARCELS_CREATED_PER_FRAME = 600;
        private const int PARCEL_SIZE = 16;
        private const int CHUNK_SIZE = 40;
        private const float MATERIAL_TILING = 0.025f;
        private readonly LandscapeData landscapeData;
        private readonly LandscapeAssetPoolManager poolManager;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly ITerrainGenerator terrainGenerator;
        private readonly Transform landscapeParentObject;
        private int concurrentJobs;
        private int parcelsCreated;
        private readonly MaterialPropertyBlock materialPropertyBlock;
        private static readonly int BASE_MAP = Shader.PropertyToID("_BaseMap");
        private static readonly int BASE_MAP_ST = Shader.PropertyToID("_BaseMap_ST");

        private LandscapeParcelInitializerSystem(World world,
            LandscapeData landscapeData,
            LandscapeAssetPoolManager poolManager,
            MapRendererTextureContainer textureContainer,
            ITerrainGenerator terrainGenerator) : base(world)
        {
            this.landscapeData = landscapeData;
            this.poolManager = poolManager;
            this.textureContainer = textureContainer;
            this.terrainGenerator = terrainGenerator;
            landscapeParentObject = new GameObject("Landscape").transform;
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        private bool b;

        protected override void Update(float t)
        {
            InitializeMapChunksQuery(World);

            //InitializeOwnedParcelFacadesQuery(World);
            parcelsCreated = 0;

            // This first query gets all LandscapeParcel and creates a Job which calculates what's going to spawn inside them
            Profiler.BeginSample("LandscapeParcelInitializerSystem.InitializeLandscapeJobs");
            //InitializeLandscapeJobsQuery(World);
            Profiler.EndSample();

            if (concurrentJobs > 0)
                b = false;

            if (concurrentJobs == 0 && !b)
            {
                poolManager.Print();
                b = true;
            }

            // This second query get's the job result and spawns all the needed objects
            Profiler.BeginSample("LandscapeParcelInitializerSystem.InitializeLandscapeSubEntities");
            //InitializeLandscapeSubEntitiesQuery(World);
            Profiler.EndSample();

        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(LandscapeParcelInitialization), typeof(SatelliteView))]
        private void InitializeMapChunks(in Entity entity)
        {
            var genesisCityOffset = new Vector3(150 * PARCEL_SIZE, 0, 150 * PARCEL_SIZE);
            var mapOffset = new Vector3(-2 * PARCEL_SIZE, 0, -(20 * PARCEL_SIZE) + 50 - 1.7f);
            var quadCenter = new Vector3(320, 0, 320);
            Vector3 zFightPrevention = Vector3.down * 0.015f;

            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    int posX = x * CHUNK_SIZE * PARCEL_SIZE;
                    int posZ = y * CHUNK_SIZE * PARCEL_SIZE;

                    Vector3 coord = new Vector3(posX, 0, posZ) - genesisCityOffset + quadCenter + mapOffset + zFightPrevention;

                    Transform groundTile = poolManager.Get(landscapeData.mapChunk);
                    groundTile.SetParent(landscapeParentObject);
                    groundTile.transform.position = coord;
                    groundTile.transform.eulerAngles = new Vector3(90, 0, 0);

                    materialPropertyBlock.SetTexture(BASE_MAP, textureContainer.GetChunk(new Vector2Int(x, 7 - y)));
                    groundTile.GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);
                    groundTile.name = $"CHUNK {x},{y}";
                }
            }

            World.Remove<LandscapeParcelInitialization>(entity);
        }

        /*[Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(LandscapeParcelInitialization))]
        private void InitializeLandscapeJobs(in Entity entity, in LandscapeParcel landscapeParcel)
        {
            // This prevents a fatal crash caused by creating too much parallel jobs, it also prevents overloading the CPU too much
            if (concurrentJobs > MAX_JOB_CONCURRENCY) return;

            foreach (LandscapeAsset landscapeAsset in landscapeData.assets)
            {
                NoiseSettings noiseSettings = landscapeAsset.noiseData.settings;

                var octaveOffsets = new NativeArray<float2>(noiseSettings.octaves, Allocator.Persistent);
                float maxPossibleHeight = Noise.CalculateOctaves(ref landscapeParcel.Random, ref noiseSettings, ref octaveOffsets);

                var parcelNoiseJob = new LandscapeParcelNoiseJob
                {
                    Parcel = entity,
                    Results = new NativeArray<float>(landscapeData.density * landscapeData.density, Allocator.Persistent),
                    OctaveOffsets = octaveOffsets,
                    MaxPossibleHeight = maxPossibleHeight,
                    LandscapeAsset = landscapeAsset,
                };

                var offset = new float2(landscapeParcel.Position.x, landscapeParcel.Position.z);

                var noiseJob = new NoiseJob(
                    ref parcelNoiseJob.Results,
                    in octaveOffsets,
                    landscapeData.density,
                    landscapeData.density,
                    in noiseSettings,
                    maxPossibleHeight,
                    offset,
                    NoiseJobOperation.SET
                );

                parcelNoiseJob.Handle = noiseJob.Schedule(landscapeData.density * landscapeData.density, 32);

                World.Create(parcelNoiseJob);
                concurrentJobs++;
            }

            World.Remove<LandscapeParcelInitialization>(entity);
        }*/

        /*[Query]
        private void InitializeLandscapeSubEntities(in Entity entity, ref LandscapeParcelNoiseJob landscapeParcelNoiseJob)
        {
            if (!landscapeParcelNoiseJob.Handle.IsCompleted) return;
            landscapeParcelNoiseJob.Handle.Complete();
            concurrentJobs--;

            // This means that the job ended and our parcel entity does not exist anymore
            if (!World.Has<LandscapeParcel>(landscapeParcelNoiseJob.Parcel))
            {
                DisposeEntityAndJob(in entity, ref landscapeParcelNoiseJob);
                return;
            }

            LandscapeParcel landscapeParcel = World.Get<LandscapeParcel>(landscapeParcelNoiseJob.Parcel);
            float dist = 16f / landscapeData.density;
            Vector3 basePos = landscapeParcel.Position;
            Vector3 parcelMargin = (Vector3.right * dist / 2) + (Vector3.forward * dist / 2);

            for (var i = 0; i < landscapeData.density; i++)
            {
                for (var j = 0; j < landscapeData.density; j++)
                {
                    int index = i + (j * landscapeData.density);

                    // This slow, can we find a workaround?
                    float objHeight = landscapeParcelNoiseJob.Results[index];

                    // We probably want to setup some thresholds for this instead of bigger than zero
                    if (objHeight > 0)
                    {
                        // TODO: draw them with BatchRenderer

                        Profiler.BeginSample("LandscapeParcelInitializerSystem.InitializeLandscapeSubEntities.SpawnObject");
                        Vector3 subEntityPos = (Vector3.right * i * dist) + (Vector3.forward * j * dist);
                        Vector3 finalPosition = basePos + parcelMargin + subEntityPos;

                        LandscapeAsset landscapeAsset = landscapeParcelNoiseJob.LandscapeAsset;
                        int randomIndex = landscapeParcel.Random.Next(0, landscapeAsset.assets.Length - 1);
                        Transform prefab = landscapeAsset.assets[randomIndex];

                        Transform objTransform = poolManager.Get(prefab);
                        objTransform.SetParent(landscapeParentObject);
                        objTransform.transform.position = finalPosition;
                        landscapeAsset.randomization.ApplyRandomness(objTransform, landscapeParcel.Random, objHeight);

                        // can we avoid this allocation? we need to keep track of them
                        if (!landscapeParcel.Assets.ContainsKey(prefab))
                            landscapeParcel.Assets.Add(prefab, new List<Transform>());

                        landscapeParcel.Assets[prefab].Add(objTransform);
                        Profiler.EndSample();

                    }
                }
            }

            // TODO: Add to unload list
            Transform groundTile = poolManager.Get(landscapeData.groundTile);
            groundTile.SetParent(landscapeParentObject);
            groundTile.transform.position = basePos + new Vector3(8, 0, 8);
            groundTile.transform.eulerAngles = new Vector3(0, -180, 0);

            //UpdateMaterialPropertyBlock(basePos);
            //groundTile.GetComponent<Renderer>().SetPropertyBlock(materialPropertyBlock);
            groundTile.name = $"Empty {basePos.x / 16:F0},{basePos.z / 16:F0}";

            DisposeEntityAndJob(in entity, ref landscapeParcelNoiseJob);

            void DisposeEntityAndJob(in Entity entity, ref LandscapeParcelNoiseJob landscapeParcelNoiseJob)
            {
                landscapeParcelNoiseJob.Results.Dispose();
                landscapeParcelNoiseJob.OctaveOffsets.Dispose();
                World.Destroy(entity);
            }
        }*/
    }
}
