using DCL.WebRequests.Dumper;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.WebRequests.Analytics.Metrics
{
    public static class MetricsRegistry
    {
        public static readonly Type[] TYPES = AppDomain.CurrentDomain.GetAssemblies()
                                                       .SelectMany(assembly => assembly.GetTypes())
                                                       .Where(type => typeof(RequestMetricBase).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericType
                                                                      && type != typeof(RequestMetricRecorder))
                                                       .ToArray();

        public static readonly Dictionary<Type, int> INDICES = TYPES.Select((i, r) => (i, r)).ToDictionary(s => s.i, s => s.r);
    }
}
