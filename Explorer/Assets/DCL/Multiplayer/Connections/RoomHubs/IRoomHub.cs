using DCL.Multiplayer.Connections.Rooms;
using LiveKit.Rooms;
using System;

namespace DCL.Multiplayer.Connections.RoomHubs
{
    public interface IRoomHub
    {
        IRoom IslandRoom();

        IRoom SceneRoom();

        void Reconnect();

        class Fake : IRoomHub
        {
            public IRoom IslandRoom() =>
                NullRoom.INSTANCE;

            public IRoom SceneRoom() =>
                NullRoom.INSTANCE;

            public void Reconnect()
            {
                //ignore
            }
        }
    }
}
