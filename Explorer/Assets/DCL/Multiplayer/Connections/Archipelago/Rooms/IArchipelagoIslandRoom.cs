using LiveKit.Rooms;

namespace DCL.Multiplayer.Connections.Credentials.Archipelago.Rooms
{
    public interface IArchipelagoIslandRoom
    {
        void Start();

        void Stop();

        bool IsRunning();

        IRoom Room();
    }

    public static class ArchipelagoIslandRoomExtensions
    {
        public static void StartIfNotRunning(this IArchipelagoIslandRoom room)
        {
            if (!room.IsRunning())
                room.Start();
        }
    }
}
