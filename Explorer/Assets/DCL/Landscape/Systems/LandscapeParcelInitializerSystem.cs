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
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class LandscapeParcelInitializerSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly int worldSeed;

        private LandscapeParcelInitializerSystem(World world, LandscapeData landscapeData) : base(world)
        {
            worldSeed = 0;
            this.landscapeData = landscapeData;
        }


        protected override void Update(float t)
        {
            InitializeLandscapeJobsQuery(World);
            InitializeLandscapeSubEntitiesQuery(World);
        }

        [Query]
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

            LandscapeParcel landscapeParcel = World.Get<LandscapeParcel>(landscapeParcelNoiseJob.Parcel);

            float dist = 16f / landscapeData.density;
            Vector3 basePos = landscapeParcel.Position;
            Vector3 baseSubPos = (Vector3.right * dist / 2) + (Vector3.forward * dist / 2);

            for (var i = 0; i < landscapeData.density; i++)
            {
                for (var j = 0; j < landscapeData.density; j++)
                {
                    Vector3 subEntityPos = (Vector3.right * i * dist) + (Vector3.forward * j * dist);
                    Vector3 finalPosition = basePos + baseSubPos + subEntityPos;

                    int index = i + (j * landscapeData.density);
                    float objHeight = landscapeParcelNoiseJob.Results[index];

                    if (objHeight > 0)
                    {
                        // TRACK ASSETS IN THE ORIGINAL ENTITY?
                        Transform objTransform = Object.Instantiate(landscapeParcelNoiseJob.LandscapeAsset.asset, finalPosition, Quaternion.identity);
                        landscapeParcelNoiseJob.LandscapeAsset.randomization.ApplyRandomness(objTransform, landscapeParcel.Random, objHeight);

                        landscapeParcel.Assets.Add(objTransform);
                        World.Create(new LandscapeEntity(finalPosition));
                    }
                }
            }

            landscapeParcelNoiseJob.Results.Dispose();
            landscapeParcelNoiseJob.OctaveOffsets.Dispose();
            World.Destroy(entity);
        }
    }
}
