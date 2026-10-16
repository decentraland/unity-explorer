using System;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Profiling
{
    /// <summary>
    ///     Aggregates the CRDT messages exchanged between a scene and the renderer per
    ///     (direction, entity, component) while a capture is running, classifying each one
    ///     by <see cref="CrdtTrafficOutcome" />. Idle, it costs the bridge up to three volatile reads per scene tick.
    ///     Written from the scene background thread, read from the Unity main thread when the capture stops.
    ///     Entries are cleared at the start of each capture.
    /// </summary>
    public sealed class CrdtTrafficProbe
    {
        private readonly object gate = new ();
        private readonly Dictionary<Key, Entry> entries = new ();

        private long capturing;
        private long batches;
        private OutcomeCounts fromScene;
        private OutcomeCounts toScene;
        private long fromSceneBytes;
        private long toSceneBytes;
        private long incomingTicks;
        private long applyTicks;
        private long outgoingTicks;

        public bool IsCapturing => DCLInterlocked.Read(ref capturing) == 1;

        /// <summary>
        ///     Clears the previous capture and starts recording. Returns false when a capture is already running.
        /// </summary>
        public bool TryStartCapture()
        {
            lock (gate)
            {
                if (capturing == 1) return false;

                batches = 0;
                fromScene = default(OutcomeCounts);
                toScene = default(OutcomeCounts);
                fromSceneBytes = 0;
                toSceneBytes = 0;
                incomingTicks = 0;
                applyTicks = 0;
                outgoingTicks = 0;

                entries.Clear();

                DCLInterlocked.Exchange(ref capturing, 1);
                return true;
            }
        }

        /// <summary>
        ///     Stops recording and copies every entry that saw traffic into <paramref name="target" />, unordered.
        /// </summary>
        public Snapshot StopCapture(List<EntrySnapshot> target)
        {
            lock (gate)
            {
                DCLInterlocked.Exchange(ref capturing, 0);

                foreach (Entry entry in entries.Values)
                {
                    if (entry.Messages == 0) continue;

                    target.Add(new EntrySnapshot(entry.Key.Direction, entry.Key.EntityId, entry.Key.ComponentId, entry.Messages, entry.Bytes, entry.Counts));
                }

                return new Snapshot(batches, fromScene, fromSceneBytes, toScene, toSceneBytes, new BridgeTime(incomingTicks, applyTicks, outgoingTicks));
            }
        }

        /// <summary>
        ///     Marks one scene-to-renderer exchange (a scene tick), the unit "per tick" cadences are computed against.
        /// </summary>
        public void RecordBatch()
        {
            if (!IsCapturing) return;

            lock (gate) { batches++; }
        }

        /// <summary>
        ///     Adds <see cref="System.Diagnostics.Stopwatch" /> ticks spent deserializing the scene's messages, reconciling
        ///     them and preparing the world sync buffer.
        /// </summary>
        public void AddIncomingTime(long stopwatchTicks)
        {
            if (!IsCapturing) return;

            lock (gate) { incomingTicks += stopwatchTicks; }
        }

        /// <summary>
        ///     Adds <see cref="System.Diagnostics.Stopwatch" /> ticks spent applying the world sync buffer while holding the scene world lock.
        /// </summary>
        public void AddApplyTime(long stopwatchTicks)
        {
            if (!IsCapturing) return;

            lock (gate) { applyTicks += stopwatchTicks; }
        }

        /// <summary>
        ///     Adds <see cref="System.Diagnostics.Stopwatch" /> ticks spent serializing the renderer's messages back to the scene.
        /// </summary>
        public void AddOutgoingTime(long stopwatchTicks)
        {
            if (!IsCapturing) return;

            lock (gate) { outgoingTicks += stopwatchTicks; }
        }

        public void Record(CrdtTrafficDirection direction, int entityId, int componentId, CrdtTrafficOutcome outcome, int bytes)
        {
            if (!IsCapturing) return;

            var key = new Key(direction, entityId, componentId);

            lock (gate)
            {
                if (!entries.TryGetValue(key, out Entry entry))
                {
                    entry = new Entry(key);
                    entries[key] = entry;
                }

                entry.Messages++;
                entry.Bytes += bytes;
                entry.Counts.Add(outcome);

                if (direction == CrdtTrafficDirection.FromScene)
                {
                    fromScene.Add(outcome);
                    fromSceneBytes += bytes;
                }
                else
                {
                    toScene.Add(outcome);
                    toSceneBytes += bytes;
                }
            }
        }

        public readonly struct Snapshot
        {
            /// <summary>
            ///     Scene ticks observed during the capture.
            /// </summary>
            public readonly long Batches;
            public readonly OutcomeCounts FromScene;
            public readonly long FromSceneBytes;
            public readonly OutcomeCounts ToScene;
            public readonly long ToSceneBytes;
            public readonly BridgeTime Time;

            public Snapshot(long batches, OutcomeCounts fromScene, long fromSceneBytes, OutcomeCounts toScene, long toSceneBytes, BridgeTime time)
            {
                Batches = batches;
                FromScene = fromScene;
                FromSceneBytes = fromSceneBytes;
                ToScene = toScene;
                ToSceneBytes = toSceneBytes;
                Time = time;
            }
        }

        /// <summary>
        ///     <see cref="System.Diagnostics.Stopwatch" /> ticks the CRDT bridge spent on the scene's exchanges during the capture,
        ///     excluding the probe's own classification work.
        /// </summary>
        public readonly struct BridgeTime
        {
            public readonly long IncomingTicks;
            public readonly long ApplyTicks;
            public readonly long OutgoingTicks;

            public long TotalTicks => IncomingTicks + ApplyTicks + OutgoingTicks;

            public BridgeTime(long incomingTicks, long applyTicks, long outgoingTicks)
            {
                IncomingTicks = incomingTicks;
                ApplyTicks = applyTicks;
                OutgoingTicks = outgoingTicks;
            }
        }

        public readonly struct EntrySnapshot
        {
            public readonly CrdtTrafficDirection Direction;
            public readonly int EntityId;
            public readonly int ComponentId;
            public readonly int Messages;
            public readonly long Bytes;
            public readonly OutcomeCounts Counts;

            public EntrySnapshot(CrdtTrafficDirection direction, int entityId, int componentId, int messages, long bytes, OutcomeCounts counts)
            {
                Direction = direction;
                EntityId = entityId;
                ComponentId = componentId;
                Messages = messages;
                Bytes = bytes;
                Counts = counts;
            }
        }

        /// <summary>
        ///     Message count per <see cref="CrdtTrafficOutcome" />.
        /// </summary>
        public struct OutcomeCounts
        {
            private int applied;
            private int redundantIdenticalData;
            private int noOpSameState;
            private int noOpOutdated;
            private int noOpEntityDeleted;
            private int filteredCreatorHub;

            public readonly int Total => applied + redundantIdenticalData + noOpSameState + noOpOutdated + noOpEntityDeleted + filteredCreatorHub;

            /// <summary>
            ///     Every message that did not change the local state.
            /// </summary>
            public readonly int Wasted => Total - applied;

            public readonly int this[CrdtTrafficOutcome outcome] =>
                outcome switch
                {
                    CrdtTrafficOutcome.Applied => applied,
                    CrdtTrafficOutcome.RedundantIdenticalData => redundantIdenticalData,
                    CrdtTrafficOutcome.NoOpSameState => noOpSameState,
                    CrdtTrafficOutcome.NoOpOutdated => noOpOutdated,
                    CrdtTrafficOutcome.NoOpEntityDeleted => noOpEntityDeleted,
                    CrdtTrafficOutcome.FilteredCreatorHub => filteredCreatorHub,
                    _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
                };

            public void Add(CrdtTrafficOutcome outcome)
            {
                switch (outcome)
                {
                    case CrdtTrafficOutcome.Applied:
                        applied++;
                        break;
                    case CrdtTrafficOutcome.RedundantIdenticalData:
                        redundantIdenticalData++;
                        break;
                    case CrdtTrafficOutcome.NoOpSameState:
                        noOpSameState++;
                        break;
                    case CrdtTrafficOutcome.NoOpOutdated:
                        noOpOutdated++;
                        break;
                    case CrdtTrafficOutcome.NoOpEntityDeleted:
                        noOpEntityDeleted++;
                        break;
                    case CrdtTrafficOutcome.FilteredCreatorHub:
                        filteredCreatorHub++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
                }
            }
        }

        private readonly struct Key : IEquatable<Key>
        {
            public readonly CrdtTrafficDirection Direction;
            public readonly int EntityId;
            public readonly int ComponentId;

            public Key(CrdtTrafficDirection direction, int entityId, int componentId)
            {
                Direction = direction;
                EntityId = entityId;
                ComponentId = componentId;
            }

            public bool Equals(Key other) =>
                Direction == other.Direction && EntityId == other.EntityId && ComponentId == other.ComponentId;

            public override bool Equals(object? obj) =>
                obj is Key other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine((byte)Direction, EntityId, ComponentId);
        }

        private sealed class Entry
        {
            public readonly Key Key;
            public int Messages;
            public long Bytes;
            public OutcomeCounts Counts;

            public Entry(Key key)
            {
                Key = key;
            }
        }
    }
}
