using System;
using System.Collections.Generic;
using Unity.Profiling;

namespace Instrumentation
{
    public class ProfilerContainer : IDisposable
    {
        public readonly struct AutoScope : IDisposable
        {
            public ProfilerRecorder Recorder { get; }

            public AutoScope(ProfilerRecorder recorder)
            {
                Recorder = recorder;
                recorder.Start();
            }

            public void Dispose()
            {
                Recorder.Stop();
            }
        }

        private readonly Dictionary<string, ProfilerRecorder> recorders;

        public ProfilerContainer(int recordsInitialCapacity = 3)
        {
            recorders = new Dictionary<string, ProfilerRecorder>(recordsInitialCapacity);
        }

        public AutoScope Scope(ProfilerCategory category, string statName, string funcName)
        {
            // Create ProfilerRecorder if it was not created before
            if (!recorders.TryGetValue(funcName, out ProfilerRecorder recorder))
                recorders[funcName] = recorder
                    = new ProfilerRecorder(category, statName, 0, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            return new AutoScope(recorder);
        }

        public AutoScope Scope(ProfilerStat stat, string funcName) =>
            Scope(stat.Category, stat.StatName, funcName);

        /// <summary>
        ///     Create a new temporary scope with a new <see cref="ProfilerRecorder" />
        /// </summary>
        public static AutoScope Scope(ProfilerStat stat, int profilerCapacity = 1) =>
            new (stat.CreateRecorder(profilerCapacity));

        public void Dispose()
        {
            foreach (ProfilerRecorder recorder in recorders.Values)
                recorder.Dispose();
        }
    }
}
