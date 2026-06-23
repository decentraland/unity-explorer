using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.LiveKit.Public;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Messaging.Throughput;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Multiplayer.Connections.Systems.Throughput;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.PortableExperiences
{
    /// <summary>
    ///     Owns, per running authoritative Portable Experience scene, a scene-level LiveKit connection
    ///     (<see cref="PortableExperienceSceneRoom" />) plus a <see cref="IMessagePipe" /> over its data channel.
    ///     Joining the scene room carries the sceneId, which makes the world-content-server spawn the authoritative
    ///     server (Hammurabi). Connections are keyed by sceneId (entity hash) and reconciled against the set of
    ///     currently-loaded authoritative scenes via <see cref="RetainOnly" />. The message pipe is exposed via
    ///     <see cref="TryGetRoom" /> so the scene comms multiplexer can route that scene's CRDT to/from the server;
    ///     this class never references the scene-runtime pipe (assembly direction is SceneRuntime → DCL.Multiplayer).
    /// </summary>
    public class PortableExperienceWorldComms : IDisposable
    {
        private readonly IWebRequestController webRequests;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IMultiPool multiPool;
        private readonly IMemoryPool memoryPool;
        private readonly Dictionary<string, Entry> entriesBySceneId = new ();
        private readonly List<string> staleSceneIdsBuffer = new ();

        public PortableExperienceWorldComms(IWebRequestController webRequests, IWeb3IdentityCache identityCache, IDecentralandUrlsSource urlsSource,
            IMultiPool multiPool, IMemoryPool memoryPool)
        {
            this.webRequests = webRequests;
            this.identityCache = identityCache;
            this.urlsSource = urlsSource;
            this.multiPool = multiPool;
            this.memoryPool = memoryPool;
        }

        public void Dispose()
        {
            foreach (Entry entry in entriesBySceneId.Values)
                DisposeEntry(entry);

            entriesBySceneId.Clear();
        }

        /// <summary>
        ///     Connects to the scene room of the experience the first time it is requested for a sceneId. Idempotent:
        ///     repeated calls for an already-connected sceneId are ignored.
        /// </summary>
        public void EnsureConnected(string sceneId, IRealmData portableExperienceRealm)
        {
            if (entriesBySceneId.ContainsKey(sceneId)) return;

            var room = new PortableExperienceSceneRoom(webRequests, identityCache, urlsSource, portableExperienceRealm, sceneId);
            IMessagePipe pipe = new MessagePipe(room.Room().DataPipe.WithThroughputMeasure(new ThroughputBufferBunch(new ThroughputBuffer(), new ThroughputBuffer())),
                                       multiPool, memoryPool, RoomSource.GATEKEEPER)
                                   .WithLog("PortableExperienceScene");

            entriesBySceneId[sceneId] = new Entry(room, pipe);

            ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Portable Experience comms: connecting scene '{sceneId}' of world '{portableExperienceRealm.RealmName}'");
            ConnectAsync(sceneId, room).Forget();
        }

        /// <summary>
        ///     Returns the LiveKit room and its message pipe for a connected scene, regardless of connection state.
        /// </summary>
        public bool TryGetRoom(string sceneId, out IConnectiveRoom room, out IMessagePipe pipe)
        {
            if (entriesBySceneId.TryGetValue(sceneId, out Entry entry))
            {
                room = entry.Room;
                pipe = entry.Pipe;
                return true;
            }

            room = null!;
            pipe = null!;
            return false;
        }

        /// <summary>
        ///     True when the scene's Portable Experience room exists and its LiveKit connection is established. Used to
        ///     report <c>RealmInfo.isConnectedSceneRoom</c> for PX scenes so the SDK initiates the CRDT handshake.
        /// </summary>
        public bool IsConnected(string sceneId) =>
            entriesBySceneId.TryGetValue(sceneId, out Entry entry)
            && entry.Room.CurrentState() == IConnectiveRoom.State.Running
            && entry.Room.Room().Info.ConnectionState == LKConnectionState.ConnConnected;

        /// <summary>
        ///     Disconnects and disposes every scene whose sceneId is no longer present in <paramref name="liveSceneIds" />.
        /// </summary>
        public void RetainOnly(ICollection<string> liveSceneIds)
        {
            if (entriesBySceneId.Count == 0) return;

            staleSceneIdsBuffer.Clear();

            foreach (string sceneId in entriesBySceneId.Keys)
                if (!liveSceneIds.Contains(sceneId))
                    staleSceneIdsBuffer.Add(sceneId);

            foreach (string sceneId in staleSceneIdsBuffer)
            {
                Entry entry = entriesBySceneId[sceneId];
                entriesBySceneId.Remove(sceneId);

                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Portable Experience comms: disconnecting scene '{sceneId}'");
                DisposeEntry(entry);
            }
        }

        private static void DisposeEntry(Entry entry)
        {
            entry.Pipe.Dispose();
            StopAndDisposeAsync(entry.Room).Forget();
        }

        private static async UniTaskVoid ConnectAsync(string sceneId, IConnectiveRoom room)
        {
            try
            {
                bool connected = await room.StartIfNotAsync();
                ReportHub.Log(ReportCategory.COMMS_SCENE_HANDLER, $"Portable Experience comms: scene '{sceneId}' connection finished, connected={connected}");
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMS_SCENE_HANDLER)); }
        }

        private static async UniTaskVoid StopAndDisposeAsync(IConnectiveRoom room)
        {
            try { await room.StopIfNotAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.COMMS_SCENE_HANDLER)); }
            finally { room.Dispose(); }
        }

        private readonly struct Entry
        {
            public readonly IConnectiveRoom Room;
            public readonly IMessagePipe Pipe;

            public Entry(IConnectiveRoom room, IMessagePipe pipe)
            {
                Room = room;
                Pipe = pipe;
            }
        }
    }
}
