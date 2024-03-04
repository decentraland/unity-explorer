using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.RoomHubs;
using Decentraland.Kernel.Comms.Rfc4;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using LiveKit.Rooms;
using System.Collections.Generic;
using Utility.Multithreading;

namespace DCL.Multiplayer.Profiles.BroadcastProfiles
{
    public class ProfileBroadcast : IProfileBroadcast
    {
        private const string TOPIC = "Topic";
        private const int CURRENT_PROFILE_VERSION = 0;
        private readonly IRoomHub roomHub;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;

        public ProfileBroadcast(IRoomHub roomHub, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.roomHub = roomHub;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        public async UniTaskVoid NotifyRemotesAsync()
        {
            await using ExecuteOnThreadPoolScope _ = await ExecuteOnThreadPoolScope.NewScopeAsync();
            using SmartWrap<AnnounceProfileVersion> versionWrap = multiPool.TempResource<AnnounceProfileVersion>();
            versionWrap.value.ProfileVersion = CURRENT_PROFILE_VERSION;
            AnnounceProfileVersion version = versionWrap.value;

            using SmartWrap<Packet> packetWrap = multiPool.TempResource<Packet>();
            Packet? packet = packetWrap.value;

            packet.ClearMessage();
            packet.ProfileVersion = version;

            using MemoryWrap memory = memoryPool.Memory(packet);
            packet.WriteTo(memory);

            NotifyRemotesAsync(roomHub.IslandRoom(), memory);
            NotifyRemotesAsync(roomHub.SceneRoom(), memory);
        }

        private static void NotifyRemotesAsync(IRoom room, MemoryWrap data)
        {
            //TODO time debounce
            //TODO remove allocation on list
            var remotes = new List<string>(room.Participants.RemoteParticipantSids());
            room.DataPipe.PublishData(data.Span(), TOPIC, remotes);
        }
    }
}
