#nullable enable

using CRDT.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Utility;

namespace CRDT.Attribution
{
    /// <summary>
    ///     Keeps the last network writer of every component of every entity, per scene.
    ///     <para>
    ///         Fed from the scene-room ingress, which runs off the main thread, and read from tool calls on the main
    ///         thread, so every access takes the same lock. The walk over the batch is read-only and independent of
    ///         the one <see cref="CRDTFilter" /> performs on the way to the scene; it applies the same drop rule
    ///         (<see cref="CRDTFilter.ShouldDropIncoming" />) so the log never claims a write the scene never saw.
    ///     </para>
    ///     Bounded on three axes: a scene keeps at most <see cref="MAX_WRITES_PER_SCENE" /> component rows and
    ///     <see cref="MAX_WRITERS_PER_SCENE" /> distinct addresses, and the log at most <see cref="MAX_SCENES" />
    ///     scenes, evicting the least recently written one. Every write either budget refuses is counted rather than
    ///     silently forgotten, so a reader can tell a quiet component from a truncated log.
    /// </summary>
    public sealed class CrdtWriterLog : ICrdtWriterLog
    {
        private const int MAX_SCENES = 8;
        private const int MAX_WRITES_PER_SCENE = 4096;
        private const int MAX_WRITERS_PER_SCENE = 128;

        /// <summary>
        ///     Monotonic and thread-safe, unlike <c>Time.realtimeSinceStartup</c>, which the ingress thread cannot read.
        /// </summary>
        private static readonly Stopwatch CLOCK = Stopwatch.StartNew();

        private readonly Dictionary<string, SceneWrites> scenes = new ();

        private volatile bool enabled;

        public bool IsEnabled => enabled;

        public void Enable()
        {
            enabled = true;
        }

        public void RecordInbound(string sceneId, string fromWalletId, bool isTrustedSource, ReadOnlySpan<byte> sdkMessage)
        {
            if (!enabled || string.IsNullOrEmpty(sceneId) || sdkMessage.Length < 2)
                return;

            if (!TryFindBatch(sdkMessage, out ReadOnlySpan<byte> batch, out bool viaStateSync))
                return;

            long nowMs = CLOCK.ElapsedMilliseconds;

            lock (scenes)
            {
                SceneWrites sceneWrites = SceneOf(sceneId, nowMs);
                Walk(batch, fromWalletId ?? string.Empty, isTrustedSource, viaStateSync, sceneWrites, nowMs);
            }
        }

        public void EntityWrites(string sceneId, int entityId, List<CrdtWrite> destination)
        {
            lock (scenes)
            {
                if (!scenes.TryGetValue(sceneId, out SceneWrites sceneWrites))
                    return;

                long nowMs = CLOCK.ElapsedMilliseconds;

                foreach (KeyValuePair<long, Write> entry in sceneWrites.Writes)
                {
                    if (EntityOf(entry.Key) != entityId)
                        continue;

                    Write write = entry.Value;

                    destination.Add(new CrdtWrite(entityId, ComponentOf(entry.Key), write.Writer, write.IsTrustedSource,
                        write.ViaStateSync, write.MessageType, write.CrdtTimestamp, SecondsSince(write.AtMs, nowMs)));
                }
            }
        }

        public void SceneWriters(string sceneId, List<CrdtWriterSummary> destination)
        {
            lock (scenes)
            {
                if (!scenes.TryGetValue(sceneId, out SceneWrites sceneWrites))
                    return;

                long nowMs = CLOCK.ElapsedMilliseconds;

                foreach (KeyValuePair<string, Writer> entry in sceneWrites.Writers)
                    destination.Add(new CrdtWriterSummary(entry.Key, entry.Value.IsTrustedSource, entry.Value.Writes,
                        entry.Value.StateSyncWrites, SecondsSince(entry.Value.LastAtMs, nowMs)));
            }
        }

        public int DroppedWrites(string sceneId)
        {
            lock (scenes) { return scenes.TryGetValue(sceneId, out SceneWrites sceneWrites) ? sceneWrites.Dropped : 0; }
        }

        /// <summary>
        ///     Locates the CRDT batch inside an SDK scene-room message, or reports that the message carries none.
        ///     Encoding at https://github.com/decentraland/js-sdk-toolchain/blob/f122eaa2acaaed80db7ee0302e8d60ca7d2337bf/packages/@dcl/sdk/src/network/message-bus-sync.ts#L177-L208
        /// </summary>
        private static bool TryFindBatch(ReadOnlySpan<byte> sdkMessage, out ReadOnlySpan<byte> batch, out bool viaStateSync)
        {
            switch ((SdkCommsMessageType)sdkMessage[0])
            {
                case SdkCommsMessageType.CRDT:
                    batch = sdkMessage[1..];
                    viaStateSync = false;
                    return true;

                // [message type][1 byte: address length][address][CRDT messages]. This is a peer answering someone's
                // request for the scene's state, so it replays writes it did not necessarily author — the rows are
                // marked so a reader testing authorship can exclude them.
                case SdkCommsMessageType.ResCRDTState:
                    int addressLength = sdkMessage[1];

                    if (sdkMessage.Length < 2 + addressLength)
                        break;

                    batch = sdkMessage[(2 + addressLength)..];
                    viaStateSync = true;
                    return true;
            }

            batch = default(ReadOnlySpan<byte>);
            viaStateSync = false;
            return false;
        }

        private static void Walk(ReadOnlySpan<byte> batch, string writer, bool isTrustedSource, bool viaStateSync, SceneWrites sceneWrites, long nowMs)
        {
            while (batch.Length > CRDTConstants.MESSAGE_HEADER_LENGTH)
            {
                uint messageLength = batch.ReadConst<uint>();
                var messageType = (CRDTMessageType)batch[4..].ReadConst<uint>();

                if (messageLength <= CRDTConstants.MESSAGE_HEADER_LENGTH)
                    break;

                if (messageType is CRDTMessageType.NONE or >= CRDTMessageType.MAX_MESSAGE_TYPE)
                    break;

                ReadOnlySpan<byte> body = batch[CRDTConstants.MESSAGE_HEADER_LENGTH..];

                if (body.Length < HeaderLength(messageType))
                    break;

                uint bodyLength = CRDTMessageTypeUtils.TypeLengthBytes(messageType, body);

                if (bodyLength == 0 || bodyLength > body.Length)
                    break;

                if (CarriesComponent(messageType))
                {
                    int entityId = body.ReadConst<int>();
                    int componentId = body[4..].ReadConst<int>();
                    int crdtTimestamp = body[8..].ReadConst<int>();

                    if (!CRDTFilter.ShouldDropIncoming(messageType, unchecked((uint)componentId), isTrustedSource))
                        sceneWrites.Record(entityId, componentId, writer, isTrustedSource, viaStateSync, messageType, crdtTimestamp, nowMs);
                }

                batch = body[(int)bodyLength..];
            }
        }

        /// <summary>Bytes of fixed header the type has before <see cref="CRDTMessageTypeUtils.TypeLengthBytes" /> may read it.</summary>
        private static int HeaderLength(CRDTMessageType messageType) =>
            messageType switch
            {
                CRDTMessageType.PUT_COMPONENT => CRDTConstants.CRDT_PUT_COMPONENT_HEADER_LENGTH,
                CRDTMessageType.APPEND_COMPONENT => CRDTConstants.CRDT_APPEND_COMPONENT_HEADER_LENGTH,
                CRDTMessageType.AUTHORITATIVE_PUT_COMPONENT => CRDTConstants.CRDT_AUTHORITATIVE_PUT_COMPONENT_HEADER_LENGTH,
                CRDTMessageType.DELETE_COMPONENT => CRDTConstants.CRDT_DELETE_COMPONENT_HEADER_LENGTH,
                CRDTMessageType.DELETE_ENTITY => CRDTConstants.CRDT_DELETE_ENTITY_HEADER_LENGTH,

                // The network variants repeat the same fields plus a 4-byte network id.
                CRDTMessageType.PUT_COMPONENT_NETWORK => CRDTConstants.CRDT_PUT_COMPONENT_HEADER_LENGTH + 4,
                CRDTMessageType.DELETE_COMPONENT_NETWORK => CRDTConstants.CRDT_DELETE_COMPONENT_HEADER_LENGTH + 4,
                CRDTMessageType.DELETE_ENTITY_NETWORK => CRDTConstants.CRDT_DELETE_ENTITY_HEADER_LENGTH + 4,
                _ => int.MaxValue,
            };

        /// <summary>Whether the body names a component, i.e. whether the write is attributable to one row.</summary>
        private static bool CarriesComponent(CRDTMessageType messageType) =>
            messageType is CRDTMessageType.PUT_COMPONENT
                or CRDTMessageType.APPEND_COMPONENT
                or CRDTMessageType.AUTHORITATIVE_PUT_COMPONENT
                or CRDTMessageType.DELETE_COMPONENT
                or CRDTMessageType.PUT_COMPONENT_NETWORK
                or CRDTMessageType.DELETE_COMPONENT_NETWORK;

        private SceneWrites SceneOf(string sceneId, long nowMs)
        {
            if (scenes.TryGetValue(sceneId, out SceneWrites existing))
            {
                existing.LastWriteAtMs = nowMs;
                return existing;
            }

            if (scenes.Count >= MAX_SCENES)
                EvictLeastRecentlyWritten();

            var created = new SceneWrites { LastWriteAtMs = nowMs };
            scenes[sceneId] = created;
            return created;
        }

        private void EvictLeastRecentlyWritten()
        {
            string? oldest = null;
            long oldestAtMs = long.MaxValue;

            foreach (KeyValuePair<string, SceneWrites> entry in scenes)
                if (entry.Value.LastWriteAtMs < oldestAtMs)
                {
                    oldestAtMs = entry.Value.LastWriteAtMs;
                    oldest = entry.Key;
                }

            if (oldest != null)
                scenes.Remove(oldest);
        }

        private static double SecondsSince(long atMs, long nowMs) =>
            Math.Max(0, nowMs - atMs) / 1000d;

        private static int EntityOf(long key) =>
            unchecked((int)(key >> 32));

        private static int ComponentOf(long key) =>
            unchecked((int)key);

        private static long KeyOf(int entityId, int componentId) =>
            ((long)entityId << 32) | unchecked((uint)componentId);

        private sealed class SceneWrites
        {
            public readonly Dictionary<long, Write> Writes = new ();
            public readonly Dictionary<string, Writer> Writers = new ();

            public long LastWriteAtMs;
            public int Dropped;

            /// <summary>
            ///     Both budgets are checked before either table is touched, so a component row and the summary of the
            ///     address behind it are always both present or both absent — otherwise an entity could name a writer
            ///     the scene-level report never mentions. Whichever budget refuses, the write is counted as dropped.
            /// </summary>
            public void Record(int entityId, int componentId, string writer, bool isTrustedSource, bool viaStateSync, CRDTMessageType messageType, int crdtTimestamp, long nowMs)
            {
                long key = KeyOf(entityId, componentId);
                bool knownWriter = Writers.TryGetValue(writer, out Writer summary);

                if ((!Writes.ContainsKey(key) && Writes.Count >= MAX_WRITES_PER_SCENE)
                    || (!knownWriter && Writers.Count >= MAX_WRITERS_PER_SCENE))
                {
                    Dropped++;
                    return;
                }

                Writes[key] = new Write(writer, isTrustedSource, viaStateSync, messageType, crdtTimestamp, nowMs);

                int writes = (knownWriter ? summary.Writes : 0) + (viaStateSync ? 0 : 1);
                int stateSyncWrites = (knownWriter ? summary.StateSyncWrites : 0) + (viaStateSync ? 1 : 0);

                Writers[writer] = new Writer(writes, stateSyncWrites, nowMs, isTrustedSource);
            }
        }

        private readonly struct Write
        {
            public readonly string Writer;
            public readonly bool IsTrustedSource;
            public readonly bool ViaStateSync;
            public readonly CRDTMessageType MessageType;
            public readonly int CrdtTimestamp;
            public readonly long AtMs;

            public Write(string writer, bool isTrustedSource, bool viaStateSync, CRDTMessageType messageType, int crdtTimestamp, long atMs)
            {
                Writer = writer;
                IsTrustedSource = isTrustedSource;
                ViaStateSync = viaStateSync;
                MessageType = messageType;
                CrdtTimestamp = crdtTimestamp;
                AtMs = atMs;
            }
        }

        private readonly struct Writer
        {
            public readonly int Writes;
            public readonly int StateSyncWrites;
            public readonly long LastAtMs;
            public readonly bool IsTrustedSource;

            public Writer(int writes, int stateSyncWrites, long lastAtMs, bool isTrustedSource)
            {
                Writes = writes;
                StateSyncWrites = stateSyncWrites;
                LastAtMs = lastAtMs;
                IsTrustedSource = isTrustedSource;
            }
        }
    }
}
