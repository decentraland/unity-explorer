using DCL.Landscape.NoiseGeneration;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;

namespace DCL.Landscape.Config.Editor
{
    [CustomEditor(typeof(CompositeNoiseData))]
    public class CompositeNoiseDataEditor : NoiseTextureGenerator
    {
        private INoiseGenerator generator;

        protected override JobHandle ScheduleJobs(int textureSize)
        {
            var data = serializedObject.targetObject as CompositeNoiseData;
            if (data == null) return default(JobHandle);

            data.settings.ValidateValues();

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
