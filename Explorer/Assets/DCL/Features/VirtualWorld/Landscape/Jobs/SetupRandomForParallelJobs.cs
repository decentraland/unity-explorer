using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape.Jobs
{
    [BurstCompile]
    public struct SetupRandomForParallelJobs : IJob
    {
        private NativeArray<Random> randoms;
        [ReadOnly] private readonly int seed;

        public SetupRandomForParallelJobs(NativeArray<Random> randoms, int seed)
        {
            this.randoms = randoms;
            this.seed = seed;
        }

        public void Execute()
        {
            int len = randoms.Length;

            for (int i = 0; i < len; i++)
            {
                var random = new Random((uint)(seed + i));
                randoms[i] = random;
            }
        }
    }
}
