using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Landscape.Components;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeParcelInitializerSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly LandscapeAssetPoolManager poolManager;
        private readonly int worldSeed;
        private readonly Transform landscapeParentObject;

        private LandscapeParcelInitializerSystem(World world, LandscapeData landscapeData, LandscapeAssetPoolManager poolManager) : base(world)
        {
            worldSeed = 0;
            this.landscapeData = landscapeData;
            this.poolManager = poolManager;
            landscapeParentObject = new GameObject("Landscape").transform;
        }

        protected override void Update(float t)
        {
            // This first query gets all LandscapeParcel and creates a Job which calculates what's going to spawn inside them
            InitializeLandscapeJobsQuery(World);

            // This second query get's the job result and spawns all the needed objects
            InitializeLandscapeSubEntitiesQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(LandscapeParcelInitialization))]
        private void InitializeLandscapeJobs(in Entity entity, in LandscapeParcel landscapeParcel)
        {
            foreach (LandscapeAsset landscapeAsset in landscapeData.assets)
            {
                NoiseSettings noiseSettings = landscapeAsset.noiseData.settings;

                var octaveOffsets = new NativeArray<float2>(noiseSettings.octaves, Allocator.Persistent);
                float maxPossibleHeight = Noise.CalculateOctaves(worldSeed, ref noiseSettings, ref octaveOffsets);

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
            }

            World.Remove<LandscapeParcelInitialization>(entity);
        }

        [Query]
        private void InitializeLandscapeSubEntities(in Entity entity, ref LandscapeParcelNoiseJob landscapeParcelNoiseJob)
        {
            if (!landscapeParcelNoiseJob.Handle.IsCompleted) return;
            landscapeParcelNoiseJob.Handle.Complete();

            // This means that the job ended and our parcel entity does not exist anymore
            if (!World.Has<LandscapeParcel>(landscapeParcelNoiseJob.Parcel))
            {
                Debug.LogWarning("IT HAPPENED!");
                DisposeEntityAndJob(in entity, ref landscapeParcelNoiseJob);
                return;
            }

            LandscapeParcel landscapeParcel = World.Get<LandscapeParcel>(landscapeParcelNoiseJob.Parcel);
            float dist = 16f / landscapeData.density;
            Vector3 basePos = landscapeParcel.Position;
            Vector3 baseSubPos = (Vector3.right * dist / 2) + (Vector3.forward * dist / 2);

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
                        Vector3 subEntityPos = (Vector3.right * i * dist) + (Vector3.forward * j * dist);
                        Vector3 finalPosition = basePos + baseSubPos + subEntityPos;

                        Transform objTransform = poolManager.Get(landscapeParcelNoiseJob.LandscapeAsset.asset);
                        objTransform.SetParent(landscapeParentObject);
                        objTransform.transform.position = finalPosition;
                        landscapeParcelNoiseJob.LandscapeAsset.randomization.ApplyRandomness(objTransform, landscapeParcel.Random, objHeight);

                        // can we avoid this allocation?
                        if (!landscapeParcel.Assets.ContainsKey(landscapeParcelNoiseJob.LandscapeAsset.asset))
                            landscapeParcel.Assets.Add(landscapeParcelNoiseJob.LandscapeAsset.asset, new List<Transform>());

                        landscapeParcel.Assets[landscapeParcelNoiseJob.LandscapeAsset.asset].Add(objTransform);
                    }
                }
            }

            DisposeEntityAndJob(in entity, ref landscapeParcelNoiseJob);

            void DisposeEntityAndJob(in Entity entity, ref LandscapeParcelNoiseJob landscapeParcelNoiseJob)
            {
                landscapeParcelNoiseJob.Results.Dispose();
                landscapeParcelNoiseJob.OctaveOffsets.Dispose();
                World.Destroy(entity);
            }
        }
    }
}
