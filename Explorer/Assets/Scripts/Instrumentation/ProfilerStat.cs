using Unity.Profiling;

namespace Instrumentation
{
    public readonly struct ProfilerStat
    {
        public readonly ProfilerCategory Category;
        public readonly string StatName;

        public ProfilerStat(ProfilerCategory category, string statName)
        {
            Category = category;
            StatName = statName;
        }

        public static implicit operator ProfilerStat((ProfilerCategory, string) tuple) =>
            new (tuple.Item1, tuple.Item2);
    }
}
