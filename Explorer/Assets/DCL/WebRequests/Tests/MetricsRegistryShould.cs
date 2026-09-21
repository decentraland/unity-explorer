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
        [SetUp]
        public void SetUp()
        {
            MetricsRegistry.Initialize();
        }

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

            CollectionAssert.AreEquivalent(discovered, MetricsRegistry.Types);
        }

        [Test]
        public void MapEveryTypeToItsPositionInTypes()
        {
            Assert.That(MetricsRegistry.Indices.Count, Is.EqualTo(MetricsRegistry.Types.Length));

            for (var i = 0; i < MetricsRegistry.Types.Length; i++)
                Assert.That(MetricsRegistry.Indices[MetricsRegistry.Types[i]], Is.EqualTo(i));
        }

        private static IEnumerable<Type> GetTypesSafely(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
        }
    }
}
