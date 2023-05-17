using JetBrains.Annotations;
using System;
using Unity.Profiling;

namespace Instrumentation
{
    public static class ProfilerStatExtensions
    {
        public static void Check(this in ProfilerStat profilerStat, [NotNull] Action @delegate, [NotNull] Action<long> checkFunc)
        {
            ProfilerContainer.AutoScope scope;

            // the same samplers are shared
            // so we need to adjust the current value if the same sampler was already called
            long valueOnStart;

            using (scope = ProfilerContainer.Scope(profilerStat))
            {
                valueOnStart = scope.Recorder.CurrentValue;
                @delegate();
            }

            try { checkFunc(scope.Recorder.CurrentValue - valueOnStart); }
            finally
            {
                scope.Recorder.Reset();
                scope.Recorder.Dispose();
            }
        }

        public static ProfilerRecorder CreateRecorder(this in ProfilerStat stat, int profilerCapacity = 0) =>
            new (stat.Category, stat.StatName, profilerCapacity, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
    }
}
