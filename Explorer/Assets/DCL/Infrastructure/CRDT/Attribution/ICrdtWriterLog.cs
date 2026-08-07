#nullable enable

using CRDT.Protocol;
using System;
using System.Collections.Generic;

namespace CRDT.Attribution
{
    /// <summary>
    ///     Records which address authored each component write that reached a scene over the scene room, so tools
    ///     can answer "who wrote this" and not only "what does it say".
    ///     <para>
    ///         A scene's CRDT state is the merge of writes from its own JavaScript and from every peer in the scene
    ///         room — including, for an authoritative game, its server. Once merged, the rows are indistinguishable:
    ///         a position the server set and a position a modified client asserted read exactly the same. Scenes
    ///         built on <c>@dcl/sdk/network</c> guard against that themselves (<c>validateBeforeChange</c> comparing
    ///         the sender against <see cref="AUTHORITATIVE_SERVER_ADDRESS" />), but nothing outside the scene could
    ///         see the provenance those guards act on. This log keeps it.
    ///     </para>
    ///     <para>
    ///         Only inbound network writes are recorded. A component the scene's own JavaScript wrote locally never
    ///         appears here, so "absent from the log" means "not written by a peer", not "not written".
    ///     </para>
    ///     Disabled until <see cref="Enable" /> is called, so the recording costs nothing in a normal session.
    /// </summary>
    public interface ICrdtWriterLog
    {
        /// <summary>The identity the authoritative server of a scene connects to the scene room under.</summary>
        public const string AUTHORITATIVE_SERVER_ADDRESS = "authoritative-server";

        bool IsEnabled { get; }

        /// <summary>Starts recording. Called by the consumer that needs the attribution (the MCP server).</summary>
        void Enable();

        /// <summary>
        ///     Records the component writes carried by one inbound scene-room message.
        /// </summary>
        /// <param name="sceneId">Scene definition id the message was addressed to.</param>
        /// <param name="fromWalletId">Address of the peer that sent it, as the transport reports it.</param>
        /// <param name="isTrustedSource">Whether the sender is the local participant or a scene admin.</param>
        /// <param name="sdkMessage">
        ///     The payload after the Explorer routing byte, i.e. starting at the SDK's own message type
        ///     (<see cref="SdkCommsMessageType" />). Messages that carry no CRDT batch are ignored.
        /// </param>
        void RecordInbound(string sceneId, string fromWalletId, bool isTrustedSource, ReadOnlySpan<byte> sdkMessage);

        /// <summary>Appends the last network write of every component of <paramref name="entityId" /> to <paramref name="destination" />.</summary>
        void EntityWrites(string sceneId, int entityId, List<CrdtWrite> destination);

        /// <summary>Appends one entry per address that has written to <paramref name="sceneId" /> to <paramref name="destination" />.</summary>
        void SceneWriters(string sceneId, List<CrdtWriterSummary> destination);

        /// <summary>Number of writes the log refused for <paramref name="sceneId" /> because one of its budgets was full.</summary>
        int DroppedWrites(string sceneId);

        /// <summary>The log a client that does not want attribution gets; every call is a no-op.</summary>
        public class Null : ICrdtWriterLog
        {
            public static readonly Null INSTANCE = new ();

            public bool IsEnabled => false;

            public void Enable() { }

            public void RecordInbound(string sceneId, string fromWalletId, bool isTrustedSource, ReadOnlySpan<byte> sdkMessage) { }

            public void EntityWrites(string sceneId, int entityId, List<CrdtWrite> destination) { }

            public void SceneWriters(string sceneId, List<CrdtWriterSummary> destination) { }

            public int DroppedWrites(string sceneId) =>
                0;
        }
    }

    /// <summary>
    ///     The last network write observed for one component of one entity.
    /// </summary>
    public readonly struct CrdtWrite
    {
        public readonly int EntityId;
        public readonly int ComponentId;

        /// <summary>
        ///     Address the write came from, or <see cref="ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS" />.
        ///     Read together with <see cref="ViaStateSync" />: on a state-sync row this is the peer that handed the
        ///     state over, which is not necessarily the peer that authored it.
        /// </summary>
        public readonly string Writer;

        public readonly bool IsTrustedSource;

        /// <summary>
        ///     True when the row came out of a peer's answer to a CRDT state request rather than a live write — a
        ///     client that joins mid-game hydrates this way. The dump replays state whoever originally wrote it,
        ///     including the authoritative server, under the identity of the peer that supplied it, so
        ///     <see cref="Writer" /> on such a row does not identify the author.
        /// </summary>
        public readonly bool ViaStateSync;

        public readonly CRDTMessageType MessageType;

        /// <summary>The CRDT Lamport timestamp of the write, which orders it against other writes to the same component.</summary>
        public readonly int CrdtTimestamp;

        public readonly double AgeSeconds;

        public CrdtWrite(int entityId, int componentId, string writer, bool isTrustedSource, bool viaStateSync, CRDTMessageType messageType, int crdtTimestamp, double ageSeconds)
        {
            EntityId = entityId;
            ComponentId = componentId;
            Writer = writer;
            IsTrustedSource = isTrustedSource;
            ViaStateSync = viaStateSync;
            MessageType = messageType;
            CrdtTimestamp = crdtTimestamp;
            AgeSeconds = ageSeconds;
        }

        /// <summary>
        ///     True only when the authoritative server itself sent this write live. A state-sync row never qualifies,
        ///     however it was originally authored, so this stays safe to test a scene's authority assumption against.
        /// </summary>
        public bool IsAuthoritativeServer =>
            !ViaStateSync && Writer == ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS;
    }

    /// <summary>
    ///     What one address has written to a scene since the log was enabled.
    /// </summary>
    public readonly struct CrdtWriterSummary
    {
        public readonly string Address;
        public readonly bool IsTrustedSource;

        /// <summary>Live writes this address made itself.</summary>
        public readonly int Writes;

        /// <summary>
        ///     Rows this address supplied by answering a CRDT state request. Counted apart from <see cref="Writes" />
        ///     because relaying a state dump is not the same claim as authoring the state in it.
        /// </summary>
        public readonly int StateSyncWrites;

        public readonly double LastWriteAgeSeconds;

        public CrdtWriterSummary(string address, bool isTrustedSource, int writes, int stateSyncWrites, double lastWriteAgeSeconds)
        {
            Address = address;
            IsTrustedSource = isTrustedSource;
            Writes = writes;
            StateSyncWrites = stateSyncWrites;
            LastWriteAgeSeconds = lastWriteAgeSeconds;
        }

        public bool IsAuthoritativeServer =>
            Address == ICrdtWriterLog.AUTHORITATIVE_SERVER_ADDRESS;
    }
}
