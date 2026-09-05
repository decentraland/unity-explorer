using System;
using Unity.Profiling.Editor;

namespace DCL.Profiling
{
    [Serializable, ProfilerModuleMetadata(JavaScriptProfilerCounters.CATEGORY_NAME)]
    public sealed class JavaScriptProfilerModule : ProfilerModule
    {
        private static readonly ProfilerCounterDescriptor[] CHART_COUNTERS =
        {
            new(JavaScriptProfilerCounters.TOTAL_HEAP_SIZE_NAME, JavaScriptProfilerCounters.CATEGORY),
            new(JavaScriptProfilerCounters.TOTAL_HEAP_SIZE_EXECUTABLE_NAME, JavaScriptProfilerCounters.CATEGORY),
            new(JavaScriptProfilerCounters.TOTAL_PHYSICAL_SIZE_NAME, JavaScriptProfilerCounters.CATEGORY),
            new(JavaScriptProfilerCounters.USED_HEAP_SIZE_NAME, JavaScriptProfilerCounters.CATEGORY),
            new(JavaScriptProfilerCounters.TOTAL_EXTERNAL_SIZE_NAME, JavaScriptProfilerCounters.CATEGORY),
            new(JavaScriptProfilerCounters.ACTIVE_ENGINES_NAME, JavaScriptProfilerCounters.CATEGORY)
        };

        public JavaScriptProfilerModule() : base(CHART_COUNTERS) { }
    }
}
