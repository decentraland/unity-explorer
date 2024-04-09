using Arch.Core;
using Arch.Core.Utils;
using Arch.System;
using NUnit.Framework;
using System.Linq;
using Unity.PerformanceTesting;

namespace ECS.PerformanceTests
{

    public partial class ECSPerformanceTests
    {

        public void Setup()
        {
            world = World.Create();
        }


        public void Dispose()
        {
            world?.Dispose();
        }

        private const int MEASUREMENTS_COUNT = 20;
        private const int ITERATIONS_COUNT = 10;

        private struct TestComponent
        {
            public int Value;
            public byte Value2;
        }

        private struct MarkerComponent
        {
            public bool Value;
        }

        private static readonly ComponentType[] LONG_ARCHETYPE =
        {
            typeof(int),
            typeof(uint),
            typeof(string),
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(long),
            typeof(ulong),
            typeof(TestComponent),
        };

        private World world;

        private static int[] GetTestSource() =>
            new[] { 1, 10, 100, 1_000, 5_000 };


        [Performance]

        public void PerformanceMeasureAddComponent(int entitiesCount)
        {
            QueryDescription cleanUpQuery = new QueryDescription().WithAll<TestComponent>();

            for (var i = 0; i < entitiesCount; i++)
                world.Create(new MarkerComponent());

            Measure.Method(() => { AddComponentQuery(world); })
                   .IterationsPerMeasurement(ITERATIONS_COUNT)
                   .MeasurementCount(MEASUREMENTS_COUNT)
                   .WarmupCount(5)
                   .CleanUp(() => world.Remove<TestComponent>(cleanUpQuery))
                   .Run();
        }

        [Query]
        [None(typeof(TestComponent))]
        private void AddComponent(in Entity entity)
        {
            world.Add(entity, new TestComponent());
        }


        [Performance]

        public void PerformanceMeasureRemoveComponent(int entitiesCount)
        {
            QueryDescription cleanUpQuery = new QueryDescription().WithNone<TestComponent>();

            for (var i = 0; i < entitiesCount; i++)
                world.Create(LONG_ARCHETYPE);

            Measure.Method(() => { RemoveComponentQuery(world); })
                   .IterationsPerMeasurement(ITERATIONS_COUNT)
                   .MeasurementCount(MEASUREMENTS_COUNT)
                   .WarmupCount(5)
                   .CleanUp(() => world.Add<TestComponent>(cleanUpQuery))
                   .Run();
        }

        [Query]
        [All(typeof(TestComponent))]
        private void RemoveComponent(in Entity entity)
        {
            world.Remove<TestComponent>(entity);
        }


        [Performance]

        public void PerformanceMeasureIteration(int entitiesCount)
        {
            ComponentType[] archetype = LONG_ARCHETYPE.Append(typeof(MarkerComponent)).ToArray();

            for (var i = 0; i < entitiesCount; i++)
                world.Create(archetype);

            Measure.Method(() => { ChangeMarkerQuery(world); })
                   .IterationsPerMeasurement(ITERATIONS_COUNT)
                   .WarmupCount(5)
                   .MeasurementCount(MEASUREMENTS_COUNT)
                   .Run();
        }

        /// <summary>
        ///     Iterates over query for which there is no matching archetype
        /// </summary>
        /// <param name="entitiesCount"></param>

        [Performance]

        public void PerformanceMeasureEmptyIteration(int entitiesCount)
        {
            for (var i = 0; i < entitiesCount; i++)
                world.Create(LONG_ARCHETYPE);

            Measure.Method(() => { ChangeMarkerQuery(world); })
                   .IterationsPerMeasurement(ITERATIONS_COUNT)
                   .WarmupCount(5)
                   .MeasurementCount(MEASUREMENTS_COUNT)
                   .Run();
        }

        [Query]
        private void ChangeMarker(ref MarkerComponent marker)
        {
            marker.Value = !marker.Value;
        }
    }
}
