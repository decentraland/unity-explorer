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
                                                       .Where(type => IsAssignableSafe(type) && !type.IsAbstract && !type.IsGenericType
                                                                      && type != typeof(RequestMetricRecorder))
                                                       .ToArray();

        public static readonly Dictionary<Type, int> INDICES = TYPES.Select((i, r) => (i, r)).ToDictionary(s => s.i, s => s.r);

        /// <summary>
        ///     IsAssignableFrom can force-load a type's full definition; under Unity 6000.5's Mono some
        ///     precompiled dependency types fail to resolve (TypeLoadException). Skip those instead of
        ///     letting the static ctor throw and take the whole request-metrics subsystem down.
        /// </summary>
        private static bool IsAssignableSafe(Type type)
        {
            try { return typeof(RequestMetricBase).IsAssignableFrom(type); }
            catch { return false; }
        }

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
