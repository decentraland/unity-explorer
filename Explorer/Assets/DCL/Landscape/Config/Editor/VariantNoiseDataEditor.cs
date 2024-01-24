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

            generator = data.GetGenerator(1, noiseGeneratorCache);
            return generator.Schedule(textureSize, 0, 0);
        }

        protected override void DisposeNativeArrays()
        {
            ClearOctaves();
            generator?.Dispose();
        }

        private void ClearOctaves() { }

        protected override NativeArray<float> GetResultNoise() =>
            generator.GetResultCopy();

        protected override void SetupNoiseArray(int textureSize) { }
    }
}
