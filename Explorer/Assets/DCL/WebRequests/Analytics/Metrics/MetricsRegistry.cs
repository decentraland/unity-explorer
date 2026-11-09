using System;
using System.Collections.Generic;

namespace DCL.WebRequests.Analytics.Metrics
{
    public static class MetricsRegistry
    {
        public static Type[] Types { get; private set; } = Array.Empty<Type>();

        public static Dictionary<Type, int> Indices { get; private set; } = new (0);

        public static void Initialize()
        {
            if (Types.Length > 0)
                return;

            // Fix ANR: Removed a reflection scan over all assemblies. Just select types we care manually.
            Types = new[]
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

            var indices = new Dictionary<Type, int>(Types.Length);

            for (var i = 0; i < Types.Length; i++)
                indices[Types[i]] = i;

            Indices = indices;
        }
    }
}
