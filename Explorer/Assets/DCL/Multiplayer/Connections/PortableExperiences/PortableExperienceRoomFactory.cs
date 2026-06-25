using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Buffers;

namespace DCL.Multiplayer.Connections.PortableExperiences
{
    /// <summary>
    ///     Stateless factory that builds a scene-level LiveKit room (<see cref="PortableExperienceSceneRoom" />) plus a
    ///     <see cref="IMessagePipe" /> over its data channel for an authoritative Portable Experience scene. The returned
    ///     room is not started; the caller owns its lifecycle (start it during the scene load, dispose it on unload).
    /// </summary>
    public class PortableExperienceRoomFactory
    {
        private readonly IWebRequestController webRequests;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IMultiPool multiPool;
        private readonly IMemoryPool memoryPool;

        public PortableExperienceRoomFactory(IWebRequestController webRequests, IWeb3IdentityCache identityCache, IDecentralandUrlsSource urlsSource)
        {
            this.webRequests = webRequests;
            this.identityCache = identityCache;
            this.urlsSource = urlsSource;
            multiPool = new DCLMultiPool();
            memoryPool = new ArrayMemoryPool(ArrayPool<byte>.Shared!);
        }

        public (IConnectiveRoom room, IMessagePipe pipe) Create(IRealmData portableExperienceRealm, string sceneId)
        {
            var room = new PortableExperienceSceneRoom(webRequests, identityCache, urlsSource, portableExperienceRealm, sceneId);
            IMessagePipe pipe = new MessagePipe(room.Room().DataPipe, multiPool, memoryPool, RoomSource.GATEKEEPER)
               .WithLog("PortableExperienceScene");

            return (room, pipe);
        }
    }
}
