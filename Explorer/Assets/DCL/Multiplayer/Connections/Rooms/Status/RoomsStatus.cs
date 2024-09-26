using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using LiveKit.Proto;
using Utility.Ownership;

namespace DCL.Multiplayer.Connections.Rooms.Status
{
    public class RoomsStatus : IRoomsStatus
    {
        private readonly IRoomHub roomHub;
        private readonly IBox<(bool use, ConnectionQuality quality)> overrideQuality;
        private readonly IReactiveProperty<ConnectionQuality> connectionQualityScene;
        private readonly IReactiveProperty<ConnectionQuality> connectionQualityIsland;

        public IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityScene => connectionQualityScene;

        public IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityIsland => connectionQualityIsland;

        public RoomsStatus(IRoomHub roomHub, IBox<(bool use, ConnectionQuality quality)> overrideQuality)
        {
            this.roomHub = roomHub;
            this.overrideQuality = overrideQuality;
            connectionQualityScene = new ReactiveProperty<ConnectionQuality>(ConnectionQuality.QualityExcellent);
            connectionQualityIsland = new ReactiveProperty<ConnectionQuality>(ConnectionQuality.QualityExcellent);
            Update();
        }

        public void Update()
        {
            (bool useOverride, ConnectionQuality quality) = overrideQuality.Value;
            connectionQualityScene.UpdateValue(useOverride ? quality : roomHub.SceneRoom().Room().Participants.LocalParticipant().ConnectionQuality);
            connectionQualityIsland.UpdateValue(useOverride ? quality : roomHub.IslandRoom().Participants.LocalParticipant().ConnectionQuality);
        }
    }
}
