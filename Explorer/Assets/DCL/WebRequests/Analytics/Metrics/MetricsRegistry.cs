using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    public static class MetricsRegistry
    {
        // Fix ANR: Removed a reflection scan over all assemblies. Just select types we care manually.
        public static readonly Type[] TYPES =
        {
            typeof(ActiveCounter),
            typeof(Total),
            typeof(TotalFailed),
            typeof(BandwidthDown),
            typeof(BandwidthUp),
            typeof(ServeTimeSmallFileAverage),
            typeof(ServeTimePerMBAverage),
            typeof(FillRateAverage),
            typeof(TimeToFirstByteAverage),
        };

        public static readonly Dictionary<Type, int> INDICES = BuildIndices();

        private static Dictionary<Type, int> BuildIndices()
        {
            var indices = new Dictionary<Type, int>(TYPES.Length);

            for (var i = 0; i < TYPES.Length; i++)
                indices[TYPES[i]] = i;

            return indices;
        }
    }
}
