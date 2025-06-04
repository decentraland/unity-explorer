#if ENABLE_PROFILER
using Unity.Profiling;

namespace DCL.Profiling
{
    public static class JavaScriptProfilerCounters
    {
        public const string CATEGORY_NAME = "JavaScript";
        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        public static readonly string TOTAL_HEAP_SIZE_NAME = "Total Heap Size";
        public static readonly string TOTAL_HEAP_SIZE_EXECUTABLE_NAME = "Total Executable Heap Size";
        public static readonly string TOTAL_PHYSICAL_SIZE_NAME = "Total Physical Memory Size";
        public static readonly string USED_HEAP_SIZE_NAME = "Used Heap Size";
        public static readonly string TOTAL_EXTERNAL_SIZE_NAME = "Total External Memory Size";
        public static readonly string ACTIVE_ENGINES_NAME = "Active Engines";

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE
            = new (CATEGORY, TOTAL_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE_EXECUTABLE
            = new (CATEGORY, TOTAL_HEAP_SIZE_EXECUTABLE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_PHYSICAL_SIZE
            = new (CATEGORY, TOTAL_PHYSICAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> USED_HEAP_SIZE
            = new (CATEGORY, USED_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_EXTERNAL_SIZE
            = new (CATEGORY, TOTAL_EXTERNAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<int> ACTIVE_ENGINES
            = new (CATEGORY, ACTIVE_ENGINES_NAME, ProfilerMarkerDataUnit.Count);
    }
}

#endif