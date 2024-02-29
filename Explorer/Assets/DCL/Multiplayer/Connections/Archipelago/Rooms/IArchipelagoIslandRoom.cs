using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Archipelago.Rooms
{
    public interface IArchipelagoIslandRoom : IConnectiveRoom
    {
        class Fake : IArchipelagoIslandRoom
        {
            public void Start()
            {
                //ignore
            }

            public void Stop()
            {
                //ignore
            }

            public State CurrentState() =>
                State.Sleep;

            public IRoom Room() =>
                NullRoom.INSTANCE;
        }
    }
}
