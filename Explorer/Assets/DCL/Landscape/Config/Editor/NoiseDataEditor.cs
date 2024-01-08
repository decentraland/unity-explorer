using DCL.Landscape.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using Random = System.Random;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(NoiseData))]
    public class NoiseDataEditor : NoiseTextureGenerator
    {
        private NativeArray<float2> octaveOffsets;
        private NativeArray<float> noiseResults;

        protected override JobHandle ScheduleJobs(int textureSize)
        {
            var data = serializedObject.targetObject as NoiseData;
            if (data == null) return default(JobHandle);

            data.settings.ValidateValues();

            octaveOffsets.Dispose();
            octaveOffsets = new NativeArray<float2>(data.settings.octaves, Allocator.Persistent);

            float maxPossibleHeight = Noise.CalculateOctaves(new Random(data.settings.seed), ref data.settings, ref octaveOffsets);

            var job = new NoiseJob
            {
                Width = textureSize,
                Height = textureSize,
                NoiseSettings = data.settings,
                OctaveOffsets = octaveOffsets,
                Result = noiseResults,
                MaxHeight = maxPossibleHeight,
            };

            return job.Schedule(textureSize * textureSize, 32);
        }

        protected override void DisposeNativeArrays()
        {
            octaveOffsets.Dispose();
            noiseResults.Dispose();
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
