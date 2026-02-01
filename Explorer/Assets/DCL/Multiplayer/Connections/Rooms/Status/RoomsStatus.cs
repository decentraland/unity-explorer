#if !NO_LIVEKIT_MODE

using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Utilities;
using LiveKit.Proto;
using Utility.Ownership;

using DCL.LiveKit.Public;

namespace DCL.Multiplayer.Connections.Rooms.Status
{
    public class RoomsStatus
    {
        private readonly IRoomHub roomHub;
        private readonly IBox<(bool use, LKConnectionQuality quality)> overrideQuality;
        private readonly IReactiveProperty<LKConnectionQuality> connectionQualityScene;
        private readonly IReactiveProperty<LKConnectionQuality> connectionQualityIsland;

        public IReadonlyReactiveProperty<LKConnectionQuality> ConnectionQualityScene => connectionQualityScene;

        public IReadonlyReactiveProperty<LKConnectionQuality> ConnectionQualityIsland => connectionQualityIsland;

        public RoomsStatus(IRoomHub roomHub, IBox<(bool use, LKConnectionQuality quality)> overrideQuality)
        {
            this.roomHub = roomHub;
            this.overrideQuality = overrideQuality;
            connectionQualityScene = new ReactiveProperty<LKConnectionQuality>(LKConnectionQuality.QualityExcellent);
            connectionQualityIsland = new ReactiveProperty<LKConnectionQuality>(LKConnectionQuality.QualityExcellent);
            Update();
        }

        public void Update()
        {
            (bool useOverride, LKConnectionQuality quality) = overrideQuality.Value;
            connectionQualityScene.UpdateValue(useOverride ? quality : roomHub.SceneRoom().Room().Participants.LocalParticipant().ConnectionQuality);
            connectionQualityIsland.UpdateValue(useOverride ? quality : roomHub.IslandRoom().Participants.LocalParticipant().ConnectionQuality);
        }
    }
}

#endif
