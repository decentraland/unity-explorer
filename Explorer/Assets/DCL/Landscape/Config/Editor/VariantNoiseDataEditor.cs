using DCL.Landscape.NoiseGeneration;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(VariantNoiseData))]
    public class VariantNoiseDataEditor : NoiseTextureGenerator
    {
        private INoiseGenerator generator;

        protected override JobHandle ScheduleJobs(int textureSize)
        {
            if (serializedObject.targetObject is not INoiseDataFactory data)
                return default(JobHandle);

            generator = data.GetGenerator(1, 0, noiseGeneratorCache);
            return generator.Schedule(new NoiseDataPointer(textureSize, 0, 0), default(JobHandle));
        }

        protected override void DisposeNativeArrays()
        {
            generator?.Dispose();
        }

        protected override NativeArray<float> GetResultNoise(int textureSize) =>
            generator.GetResult(new NoiseDataPointer(textureSize, 0, 0));
    }
}
