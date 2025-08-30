using Unity.Jobs;

namespace DCL.Landscape.Utils
{
    public static class JobExtensions
    {
        public static int GetBatchSize(int arrayLength) =>
            (arrayLength / Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount) + 1;
    }

    public static class JobParallelForExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, int arrayLength,
            JobHandle dependsOn = default) where T : struct, IJobParallelFor =>
            jobData.Schedule(arrayLength, JobExtensions.GetBatchSize(arrayLength), dependsOn);
    }

    public static class JobParallelForBatchExtensions
    {
        public static JobHandle Schedule<T>(this T jobData, int arrayLength,
            JobHandle dependsOn = default) where T : struct, IJobParallelForBatch =>
            jobData.Schedule(arrayLength, JobExtensions.GetBatchSize(arrayLength), dependsOn);
    }
}
