using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using System;
using System.Collections.Generic;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class ProfileBroadcast : IProfileBroadcast
    {
        private readonly IRoomHub roomHub;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private const string TOPIC = "Topic";
        private const int CURRENT_VERSION = 1;

        public ProfileBroadcast(IRoomHub roomHub, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.roomHub = roomHub;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        public void NotifyRemotes()
        {
            using var versionWrap = multiPool.TempResource<AnnounceProfileVersion>();
            versionWrap.value.ProfileVersion = CURRENT_VERSION;
            var version = versionWrap.value;

            using var memory = memoryPool.Memory(version);
            version.WriteTo(memory);
            var span = memory.Span();

            NotifyRemotes(roomHub.IslandRoom(), span);
            NotifyRemotes(roomHub.SceneRoom(), span);
        }

        private static void NotifyRemotes(IRoom room, Span<byte> data)
        {
            //TODO time debounce
            //TODO remove allocation on list
            var remotes = new List<string>(room.Participants.RemoteParticipantSids());
            room.DataPipe.PublishData(data, TOPIC, remotes);
        }
    }
}
