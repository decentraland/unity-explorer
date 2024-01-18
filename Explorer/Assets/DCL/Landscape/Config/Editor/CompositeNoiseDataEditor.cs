using DCL.Landscape.Jobs;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using Random = System.Random;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(CompositeNoiseData))]
    public class CompositeNoiseDataEditor : NoiseTextureGenerator
    {
        private List<NativeArray<float2>> octaveOffsets;
        private NativeArray<float> noiseResults;

        protected override JobHandle ScheduleJobs(int textureSize)
        {
            var data = serializedObject.targetObject as CompositeNoiseData;
            if (data == null) return default(JobHandle);

            data.baseData.settings.ValidateValues();

            ClearOctaves();
            octaveOffsets = new List<NativeArray<float2>>();

            var baseOffsets = new NativeArray<float2>(data.baseData.settings.octaves, Allocator.Persistent);
            float maxPossibleHeight = Noise.CalculateOctaves(new Random(), ref data.baseData.settings, ref baseOffsets);
            octaveOffsets.Add(baseOffsets);

            var mainJob = new NoiseJob
            {
                Width = textureSize,
                Height = textureSize,
                NoiseSettings = data.baseData.settings,
                OctaveOffsets = baseOffsets,
                Result = noiseResults,
                MaxHeight = maxPossibleHeight,
            };

            JobHandle baseJob = mainJob.Schedule(textureSize * textureSize, 32);

            foreach (NoiseSettings noiseSettings in data.add)
            {
                NoiseJob job = CreateJob(noiseSettings, NoiseJobOperation.ADD);
                baseJob = job.Schedule(textureSize * textureSize, 32, baseJob);
            }

            foreach (NoiseSettings noiseSettings in data.multiply)
            {
                NoiseJob job = CreateJob(noiseSettings, NoiseJobOperation.MULTIPLY);
                baseJob = job.Schedule(textureSize * textureSize, 32, baseJob);
            }

            foreach (NoiseSettings noiseSettings in data.subtract)
            {
                NoiseJob job = CreateJob(noiseSettings, NoiseJobOperation.SUBTRACT);
                baseJob = job.Schedule(textureSize * textureSize, 32, baseJob);
            }

            return baseJob;

            NoiseJob CreateJob(NoiseSettings noiseSettings, NoiseJobOperation operation)
            {
                var offsets = new NativeArray<float2>(noiseSettings.octaves, Allocator.Persistent);
                float maxHeight = Noise.CalculateOctaves(new Random(), ref noiseSettings, ref offsets);
                octaveOffsets.Add(baseOffsets);

                return new NoiseJob(ref noiseResults,
                    in offsets,
                    textureSize,
                    textureSize,
                    in noiseSettings,
                    maxHeight,
                    new float2(0, 0),
                    operation);
            }
        }

        protected override void DisposeNativeArrays()
        {
            ClearOctaves();
            noiseResults.Dispose();
        }

        private void ClearOctaves()
        {
            /*if (octaveOffsets != null)
                foreach (NativeArray<float2> octaveOffset in octaveOffsets)
                    octaveOffset.Dispose();*/

            octaveOffsets = null;
        }

        protected override NativeArray<float> GetResultNoise() =>
            noiseResults;

        protected override void SetupNoiseArray(int textureSize)
        {
            noiseResults.Dispose();
            noiseResults = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
        }
    }
}
