using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.Settings;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Tests.PerformanceTests
{
    /// <summary>
    ///     Allocation guard for <c>MultiplayerMovementSettings.MoveKindByDistance</c>. The property returns a
    ///     cached static <c>Dictionary&lt;MovementKind,float&gt;</c> instance rather than allocating a fresh
    ///     2-entry dictionary on every get; <c>RemotePlayersMovementSystem.AccelerateVerySlowTransition</c> reads
    ///     it twice per qualifying interpolation segment per remote player (the
    ///     <c>TotalDuration &gt;= AccelerationTimeThreshold</c> branch), so an allocating getter here would churn
    ///     a dictionary on a per-frame path.
    /// </summary>
    [Category("Performance")]
    public class MultiplayerMovementSettingsAllocationPerformanceTest
    {
        private MultiplayerMovementSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<MultiplayerMovementSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }

        [Test]
        public void MoveKindByDistanceReturnsTheSameInstanceEveryGet()
        {
            Assert.That(ReferenceEquals(settings.MoveKindByDistance, settings.MoveKindByDistance), Is.True,
                "MoveKindByDistance must return a cached instance; the old `=> new() {…}` allocated a fresh dictionary per get.");

            Assert.That(settings.MoveKindByDistance[MovementKind.Walk], Is.EqualTo(1f));
            Assert.That(settings.MoveKindByDistance[MovementKind.Jog], Is.EqualTo(2f));
            Assert.That(settings.MoveKindByDistance.Count, Is.EqualTo(2));
        }

        [Test]
        public void MoveKindByDistanceCannotBeMutatedThroughTheSharedReference()
        {
            IReadOnlyDictionary<MovementKind, float> map = settings.MoveKindByDistance;

            // The cached instance is shared across every settings object, so it must reject writes:
            // casting to the mutable interface and mutating has to throw rather than corrupt the shared cache.
            Assert.That(map, Is.InstanceOf<IDictionary<MovementKind, float>>(),
                "The shared MoveKindByDistance must surface the mutable interface so a rogue caster is caught at runtime.");

            var asMutable = (IDictionary<MovementKind, float>)map;
            Assert.Throws<NotSupportedException>(() => asMutable[MovementKind.Walk] = 99f);
            Assert.Throws<NotSupportedException>(() => asMutable.Clear());
            Assert.Throws<NotSupportedException>(() => asMutable.Remove(MovementKind.Jog));

            Assert.That(settings.MoveKindByDistance[MovementKind.Walk], Is.EqualTo(1f),
                "The shared cache must be unchanged after rejected mutation attempts.");
            Assert.That(settings.MoveKindByDistance.Count, Is.EqualTo(2));
        }

        [Test, Performance]
        public void AccelerateVerySlowTransitionMoveKindLookupIsAllocationFree()
        {
            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            var sink = 0f;

            Measure.Method(() =>
                   {
                       for (var i = 0; i < 50; i++)
                       {
                           sink += settings.MoveKindByDistance[MovementKind.Walk];
                           sink += settings.MoveKindByDistance[MovementKind.Jog];
                       }
                   })
                   .WarmupCount(10)
                   .MeasurementCount(30)
                   .GC()
                   .Run();

            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Debug.Log($"[MoveKindByDistance] 50× (Walk+Jog) lookups — GC.Alloc last frame: {gcBytes} bytes (sink={sink})");
            Assert.That(gcBytes, Is.EqualTo(0),
                $"AccelerateVerySlowTransition's MoveKindByDistance lookups must be allocation-free; measured {gcBytes} bytes. " +
                "The old `=> new()` property allocated a Dictionary on every get.");
        }
    }
}
