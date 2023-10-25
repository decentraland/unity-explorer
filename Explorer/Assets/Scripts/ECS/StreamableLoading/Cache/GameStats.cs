using Unity.Profiling;

namespace ECS.StreamableLoading.Cache
{
    public class GameStats
    {
        private static readonly ProfilerCategory MyProfilerCategory = ProfilerCategory.Memory;
        public static readonly ProfilerCounter<int> EnemyCount = new (MyProfilerCategory, "AB Cache Size Calls", ProfilerMarkerDataUnit.Count);

        public static ProfilerCounterValue<int> BulletCount =
            new (MyProfilerCategory, "AB Cache Size",
                ProfilerMarkerDataUnit.Count, ProfilerCounterOptions.FlushOnEndOfFrame);

        // ---------------
    }
}
