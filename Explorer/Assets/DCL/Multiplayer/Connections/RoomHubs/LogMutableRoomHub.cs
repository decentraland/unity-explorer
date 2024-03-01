using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public class LogMutableRoomHub : IMutableRoomHub
    {
        private readonly IMutableRoomHub origin;
        private readonly Action<string> log;

        public LogMutableRoomHub(IMutableRoomHub origin, Action<string> log)
        {
            this.origin = origin;
            this.log = log;
        }

        public IRoom IslandRoom()
        {
            log("request for island room started");
            return origin.IslandRoom();
        }

        public IRoom SceneRoom()
        {
            log("request for scene room started");
            return origin.SceneRoom();
        }

        public void AssignIslandRoom(IRoom playRoom)
        {
            log("assigning island room");
            origin.AssignIslandRoom(playRoom);
        }

        public void AssignSceneRoom(IRoom playRoom)
        {
            log("assigning scene room");
            origin.AssignSceneRoom(playRoom);
        }
    }
}
