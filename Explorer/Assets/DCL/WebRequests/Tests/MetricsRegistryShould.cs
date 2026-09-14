using DCL.WebRequests.Analytics.Metrics;
using DCL.WebRequests.Dumper;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DCL.WebRequests.Tests
{
    public class MetricsRegistryShould
    {
        [Test]
        public void ContainEveryConcreteRequestMetricInTheProject()
        {
            Type[] discovered = AppDomain.CurrentDomain.GetAssemblies()
                                         .SelectMany(GetTypesSafely)
                                         .Where(type => typeof(RequestMetricBase).IsAssignableFrom(type)
                                                        && !type.IsAbstract
                                                        && !type.IsGenericType
                                                        && type != typeof(RequestMetricRecorder))
                                         .ToArray();

            CollectionAssert.AreEquivalent(discovered, MetricsRegistry.TYPES);
        }

        [Test]
        public void MapEveryTypeToItsPositionInTypes()
        {
            Assert.That(MetricsRegistry.INDICES.Count, Is.EqualTo(MetricsRegistry.TYPES.Length));

            for (var i = 0; i < MetricsRegistry.TYPES.Length; i++)
                Assert.That(MetricsRegistry.INDICES[MetricsRegistry.TYPES[i]], Is.EqualTo(i));
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }
    }
}
