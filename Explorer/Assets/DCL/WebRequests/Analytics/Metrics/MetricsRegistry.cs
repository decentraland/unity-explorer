using DCL.WebRequests.Dumper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DCL.WebRequests.Analytics.Metrics
{
    public static class MetricsRegistry
    {
        public static readonly Type[] TYPES = AppDomain.CurrentDomain.GetAssemblies()
                                                       .SelectMany(GetTypesSafely)
                                                       .Where(type => typeof(RequestMetricBase).IsAssignableFrom(type) && !type.IsAbstract && !type.IsGenericType
                                                                      && type != typeof(RequestMetricRecorder))
                                                       .ToArray();

        public static readonly Dictionary<Type, int> INDICES = TYPES.Select((i, r) => (i, r)).ToDictionary(s => s.i, s => s.r);

        /// <summary>
        ///     Safely gets types from an assembly, handling ReflectionTypeLoadException
        ///     which can occur when some types have unresolvable dependencies.
        /// </summary>
        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                // Return only the types that were successfully loaded (non-null)
                return ex.Types.Where(t => t != null);
            }
        }
    }
}
