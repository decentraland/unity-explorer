using DCL.WebRequests.Dumper;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace DCL.WebRequests.Analytics.Metrics
{
    public static class MetricsRegistry
    {
        public static readonly Type[] TYPES = CollectMetricTypes();

        public static readonly Dictionary<Type, int> INDICES = BuildIndices(TYPES);

        private static Type[] CollectMetricTypes()
        {
            var metricTypes = new List<Type>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type?[] assemblyTypes;

                // Some types may have unresolvable dependencies; keep the ones that did load.
                try { assemblyTypes = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { assemblyTypes = e.Types; }

                foreach (Type? type in assemblyTypes)
                {
                    if (type != null && IsMetricType(type))
                        metricTypes.Add(type);
                }
            }

            return metricTypes.ToArray();
        }

        /// <summary>
        ///     Inspecting a type forces it to finish loading, which throws when its base types cannot be resolved.
        ///     A broken or duplicated assembly in the domain must not take the whole registry down with it.
        /// </summary>
        private static bool IsMetricType(Type type)
        {
            try
            {
                return typeof(RequestMetricBase).IsAssignableFrom(type)
                       && !type.IsAbstract
                       && !type.IsGenericType
                       && type != typeof(RequestMetricRecorder);
            }
            catch (TypeLoadException) { return false; }
        }

        private static Dictionary<Type, int> BuildIndices(Type[] types)
        {
            var indices = new Dictionary<Type, int>(types.Length);

            for (var i = 0; i < types.Length; i++)
                indices[types[i]] = i;

            return indices;
        }
    }
}
