using DCL.Utilities;
using LiveKit.Proto;

namespace DCL.Multiplayer.Connections.Rooms.Status
{
    public interface IRoomsStatus
    {
        IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityScene { get; }

        IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityIsland { get; }
    }
}
