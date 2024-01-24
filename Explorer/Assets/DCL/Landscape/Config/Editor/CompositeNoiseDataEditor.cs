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

            generator = data.GetGenerator(1, noiseGeneratorCache);
            return generator.Schedule(textureSize, 0, 0);
        }

        protected override void DisposeNativeArrays()
        {
            ClearOctaves();
            generator?.Dispose();
        }

        private void ClearOctaves()
        {

        }

        protected override NativeArray<float> GetResultNoise() =>
            generator.GetResultCopy();

        protected override void SetupNoiseArray(int textureSize)
        {

        }
    }
}
