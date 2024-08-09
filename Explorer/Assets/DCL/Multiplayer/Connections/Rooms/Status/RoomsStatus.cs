using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using LiveKit.Proto;

namespace DCL.Multiplayer.Connections.Rooms.Status
{
    public class RoomsStatus : IRoomsStatus
    {
        private readonly IRoomHub roomHub;
        private readonly IReactiveProperty<ConnectionQuality> connectionQualityScene;
        private readonly IReactiveProperty<ConnectionQuality> connectionQualityIsland;

        public IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityScene => connectionQualityScene;

        public IReadonlyReactiveProperty<ConnectionQuality> ConnectionQualityIsland => connectionQualityIsland;

        public RoomsStatus(IRoomHub roomHub)
        {
            this.roomHub = roomHub;
            connectionQualityScene = new ReactiveProperty<ConnectionQuality>(ConnectionQuality.QualityExcellent);
            connectionQualityIsland = new ReactiveProperty<ConnectionQuality>(ConnectionQuality.QualityExcellent);
            Update();
        }

        public void Update()
        {
            connectionQualityScene.UpdateValue(roomHub.SceneRoom().Participants.LocalParticipant().ConnectionQuality);
            connectionQualityIsland.UpdateValue(roomHub.IslandRoom().Participants.LocalParticipant().ConnectionQuality);
        }
    }
}
