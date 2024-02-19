using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom
    {
        void Start();

        void Stop();

        bool IsRunning();

        IRoom Room();

        class Fake : IGateKeeperSceneRoom
        {
            public void Start()
            {
                //ignore
            }

            public void Stop()
            {
                //ignore
            }

            public bool IsRunning() =>
                true;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }

    public static class GateKeeperSceneRoomExtensions
    {
        public static void StartIfNotRunning(this IGateKeeperSceneRoom room)
        {
            if (!room.IsRunning())
                room.Start();
        }
    }
}
