using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Archipelago.Rooms.Chat;
using DCL.Multiplayer.Connections.GateKeeper.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
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

        bool TryGetUser(string wallet, out LKParticipant? participant, out IRoom? room);

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

        /// <summary>
        ///     True when the scene comms for the given scene are settled: the room is connected for that scene,
        ///     or it is in a state in which it will never connect (deactivated, offline realm comms, forbidden access)
        /// </summary>
        public static bool IsSceneRoomSettled(this IRoomHub roomHub, string sceneId)
        {
            IGateKeeperSceneRoom sceneRoom = roomHub.SceneRoom();

            if (!sceneRoom.Activated || sceneRoom.IsCommsOffline || sceneRoom.AttemptToConnectState is AttemptToConnectState.FORBIDDEN_ACCESS)
                return true;

            return sceneRoom.IsSceneConnected(sceneId);
        }

        public static int ParticipantsCount(this IRoomHub roomHub) =>
            roomHub.AllLocalRoomsRemoteParticipantIdentities().Count;

        /// <summary>
        /// Room used for the video streaming
        /// </summary>
        public static IRoom StreamingRoom(this IRoomHub roomHub) =>
            roomHub.SceneRoom().Room();
    }
}
