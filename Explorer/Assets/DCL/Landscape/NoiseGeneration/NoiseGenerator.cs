using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.Landscape.NoiseGeneration
{
    public class NoiseGenerator : BaseNoiseGenerator
    {
        public NoiseGenerator(NoiseData noiseData, uint variantSeed, uint baseSeed) : base(noiseData, variantSeed, baseSeed) { }

        protected override JobHandle OnSchedule(NoiseDataPointer noiseDataPointer, JobHandle parentJobHandle, int batchCount)
        {
            if (noiseData == null) return default(JobHandle);

            var noiseJob = new NoiseJob(noiseResultDictionary[noiseDataPointer],
                in offsets,
                noiseDataPointer.size,
                noiseDataPointer.size,
                in noiseData.settings, maxHeight,
                new float2(noiseDataPointer.offsetX, noiseDataPointer.offsetZ),
                NoiseJobOperation.SET);

            return noiseJob.Schedule(noiseDataPointer.size * noiseDataPointer.size, batchCount, parentJobHandle);
        }

        public override bool IsRecursive(NoiseDataBase otherNoiseData) =>
            noiseData == otherNoiseData;
    }
}
