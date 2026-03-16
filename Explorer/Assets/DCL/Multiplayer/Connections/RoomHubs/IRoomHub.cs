#if !NO_LIVEKIT_MODE

using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Proto;
using LiveKit.Rooms;
using System.Collections.Generic;
using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();
        IGateKeeperSceneRoom SceneRoom();
        IRoom ChatRoom();
        VoiceChatActivatableConnectiveRoom VoiceChatRoom();

        UniTask<bool> StartAsync();
        UniTask StopAsync();
        UniTask StopLocalRoomsAsync();

        IReadOnlyCollection<string> AllLocalRoomsRemoteParticipantIdentities();
    }

    public static class RoomHubExtensions
    {
        public static bool HasAnyRoomConnected(this IRoomHub roomHub) =>
            roomHub.IslandRoom().Info.ConnectionState == LKConnectionState.ConnConnected ||
            roomHub.SceneRoom().Room().Info.ConnectionState == LKConnectionState.ConnConnected;

        public static int ParticipantsCount(this IRoomHub roomHub) =>
            roomHub.AllLocalRoomsRemoteParticipantIdentities().Count;

        /// <summary>
        /// Room used for the video streaming
        /// </summary>
        public static IRoom StreamingRoom(this IRoomHub roomHub) =>
            roomHub.SceneRoom().Room();
    }
}

#endif
