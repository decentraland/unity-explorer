using Arch.Core;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Pool;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Tests.PerformanceTests
{
    /// <summary>
    ///     Allocation guards for the per-peer movement inbox. <c>RemotePlayerMovementComponent</c> backs its inbox
    ///     with the fixed-capacity <see cref="BoundedNetworkMessageQueue"/> rather than a general-purpose
    ///     <c>SimplePriorityQueue&lt;NetworkMovementMessage,double&gt;</c>, which allocates a node plus a per-item
    ///     <c>List&lt;SimpleNode&gt;</c> and hashes the whole 15-field struct on every Enqueue — a per-message cost
    ///     paid at ~10 Hz per remote player.
    ///     <para>
    ///         Ordering and overflow semantics are pinned against <c>SimplePriorityQueue</c> as an oracle in
    ///         <see cref="DequeueOrderIsAscending_AndOverflowMatchesOldSimplePriorityQueue"/>.
    ///     </para>
    /// </summary>
    [Category("Performance")]
    public class MovementInboxAllocationPerformanceTest
    {
        private const int PEERS = 50;
        private const int MAX_MESSAGES = 10;

        private static ObjectPool<SimplePriorityQueue<NetworkMovementMessage, double>> NewQueuePool() =>
            new (() => new SimplePriorityQueue<NetworkMovementMessage, double>());

        private static NetworkMovementMessage Msg(double timestamp) =>
            new () { timestamp = timestamp, position = new Vector3((float)timestamp, 0f, 0f) };

        [Test, Performance]
        public void PerPeerInboxEnqueueDequeueCycleIsAllocationFree()
        {
            var pool = NewQueuePool();
            var components = new RemotePlayerMovementComponent[PEERS];

            for (var p = 0; p < PEERS; p++)
                components[p] = new RemotePlayerMovementComponent(pool);

            ProfilerRecorder gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc");

            Measure.Method(() =>
                   {
                       for (var p = 0; p < PEERS; p++)
                       {
                           for (var m = 0; m < MAX_MESSAGES + 2; m++)
                               components[p].Enqueue(Msg((p * 1000) + m));

                           BoundedNetworkMessageQueue inbox = components[p].Queue!;

                           while (inbox.Count > 0)
                               inbox.Dequeue();
                       }
                   })
                   .WarmupCount(10)
                   .MeasurementCount(30)
                   .GC()
                   .Run();

            long gcBytes = gcAlloc.LastValue;
            gcAlloc.Dispose();

            Debug.Log($"[MovementInbox] {PEERS} peers × {MAX_MESSAGES + 2} enqueue + full drain — GC.Alloc last frame: {gcBytes} bytes");
            Assert.That(gcBytes, Is.EqualTo(0),
                $"Per-peer inbox enqueue/dequeue must be allocation-free; measured {gcBytes} bytes. " +
                "The old SimplePriorityQueue inbox allocated a node + a per-item List (and hashed the 15-field struct) on every Enqueue.");
        }

        [Test]
        public void DequeueOrderIsAscending_AndOverflowMatchesOldSimplePriorityQueue()
        {
            var rng = new System.Random(0xC0FFEE);

            for (var trial = 0; trial < 300; trial++)
            {
                int n = rng.Next(1, 3 * MAX_MESSAGES);

                var timestamps = new double[n];
                for (var i = 0; i < n; i++) timestamps[i] = i;
                for (int i = n - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (timestamps[i], timestamps[j]) = (timestamps[j], timestamps[i]);
                }

                var bounded = new BoundedNetworkMessageQueue(MAX_MESSAGES);

                var oracle = new SimplePriorityQueue<NetworkMovementMessage, double>();

                foreach (double ts in timestamps)
                {
                    NetworkMovementMessage m = Msg(ts);

                    bounded.Enqueue(m);

                    while (oracle.Count > MAX_MESSAGES) oracle.Dequeue();
                    oracle.Enqueue(m, m.timestamp);
                }

                var boundedDrain = new List<double>();
                while (bounded.Count > 0) boundedDrain.Add(bounded.Dequeue().timestamp);

                var oracleDrain = new List<double>();
                while (oracle.Count > 0) oracleDrain.Add(oracle.Dequeue().timestamp);

                for (var i = 1; i < boundedDrain.Count; i++)
                    Assert.That(boundedDrain[i], Is.GreaterThan(boundedDrain[i - 1]),
                        $"trial {trial}: dequeue order must be ascending by timestamp");

                CollectionAssert.AreEqual(oracleDrain, boundedDrain,
                    $"trial {trial}: n={n} — bounded queue must drain identically to the old SimplePriorityQueue inbox");

                Assert.That(boundedDrain.Count, Is.EqualTo(Math.Min(n, MAX_MESSAGES + 1)),
                    $"trial {trial}: survivor count must equal the old queue's high-water mark (MAX_MESSAGES + 1)");
            }
        }

        [Test, Performance]
        public void DrainToEntitiesAtSteadyStateIsAllocationFree()
        {
            const int WARMUP_FRAMES = 30;
            const int MEASURED_FRAMES = 600;
            const int MSGS_PER_PEER_PER_FRAME = 2;

            World world = World.Create();

            try
            {
                var table = new FakeEntityParticipantTable();
                var inbox = new MovementInbox(table, world);
                var pool = NewQueuePool();

                var wallets = new string[PEERS];
                var components = new RemotePlayerMovementComponent[PEERS];

                for (var p = 0; p < PEERS; p++)
                {
                    wallets[p] = "0x" + p.ToString("x40");
                    var component = new RemotePlayerMovementComponent(pool);
                    Entity entity = world.Create(component);
                    components[p] = component;
                    table.Add(wallets[p], entity);
                }

                var group = new SampleGroup("DrainToEntities.ManagedAlloc", SampleUnit.Byte, increaseIsBetter: false);
                long steadyAllocated = 0;
                double ts = 0;

                for (var frame = 0; frame < WARMUP_FRAMES + MEASURED_FRAMES; frame++)
                {
                    for (var p = 0; p < PEERS; p++)
                    for (var k = 0; k < MSGS_PER_PEER_PER_FRAME; k++)
                        inbox.Enqueue(Msg(ts += 1.0), wallets[p]);

                    long before = GC.GetAllocatedBytesForCurrentThread();
                    inbox.DrainToEntities();
                    long delta = GC.GetAllocatedBytesForCurrentThread() - before;

                    if (frame >= WARMUP_FRAMES)
                    {
                        steadyAllocated += delta;
                        Measure.Custom(group, delta);
                    }

                    for (var p = 0; p < PEERS; p++)
                    {
                        BoundedNetworkMessageQueue q = components[p].Queue!;
                        while (q.Count > 0) q.Dequeue();
                    }
                }

                Debug.Log($"[MovementInbox.DrainToEntities] {PEERS} peers, steady-state managed alloc over {MEASURED_FRAMES} frames: {steadyAllocated} bytes");
                Assert.That(steadyAllocated, Is.EqualTo(0),
                    $"DrainToEntities must be allocation-free at steady state; measured {steadyAllocated} bytes over {MEASURED_FRAMES} frames. " +
                    "The old inbox allocated 2 objects per message per peer via SimplePriorityQueue.Enqueue.");
            }
            finally
            {
                world.Dispose();
            }
        }

        /// <summary>
        ///     Zero-allocation, dictionary-backed <see cref="IReadOnlyEntityParticipantTable"/> for the drain path.
        ///     A real fake (not an NSubstitute mock) on purpose: substitute call routing allocates per invocation and
        ///     would swamp the managed-alloc delta the test is trying to pin to <c>DrainToEntities</c>.
        /// </summary>
        private sealed class FakeEntityParticipantTable : IReadOnlyEntityParticipantTable
        {
            private readonly Dictionary<string, Entity> map = new ();

            public void Add(string wallet, Entity entity) => map[wallet] = entity;

            public int Count => map.Count;

            public IReadOnlyEntityParticipantTable.Entry Get(string walletId) =>
                new (walletId, map[walletId], RoomSource.Pulse);

            public bool TryGet(string walletId, out IReadOnlyEntityParticipantTable.Entry entry)
            {
                if (map.TryGetValue(walletId, out Entity entity))
                {
                    entry = new IReadOnlyEntityParticipantTable.Entry(walletId, entity, RoomSource.Pulse);
                    return true;
                }

                entry = default;
                return false;
            }

            public bool Has(string walletId) => map.ContainsKey(walletId);

            public IReadOnlyCollection<string> Wallets() => map.Keys;
        }
    }
}
