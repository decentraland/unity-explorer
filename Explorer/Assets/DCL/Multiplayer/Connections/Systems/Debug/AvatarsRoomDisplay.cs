using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    /// <summary>
    ///     Reconciles the avatar roster against LiveKit's participant rosters. The two are allowed to differ - the
    ///     Island room spans a wider area than the transport that creates avatars - but the overlap is what says
    ///     the avatars on screen are backed by live LiveKit sessions.
    /// </summary>
    public class AvatarsRoomDisplay : IRoomDisplay
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IRoomHub roomHub;
        private readonly ElementBinding<string> activeCount;
        private readonly ElementBinding<string> onLiveKitCount;
        private readonly ElementBinding<string> offLiveKitCount;
        private readonly ElementBinding<string> withoutAvatarCount;

        internal readonly ElementBinding<bool> debugAvatarsRooms;

        public AvatarsRoomDisplay(IReadOnlyEntityParticipantTable entityParticipantTable, IRoomHub roomHub, DebugWidgetBuilder widgetBuilder)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.roomHub = roomHub;
            activeCount = new ElementBinding<string>(string.Empty);
            onLiveKitCount = new ElementBinding<string>(string.Empty);
            offLiveKitCount = new ElementBinding<string>(string.Empty);
            withoutAvatarCount = new ElementBinding<string>(string.Empty);
            debugAvatarsRooms = new ElementBinding<bool>(false);

            widgetBuilder
               .AddCustomMarker("Active Avatars", activeCount)
               .AddCustomMarker("Avatars on LiveKit", onLiveKitCount)
               .AddCustomMarker("Avatars off LiveKit", offLiveKitCount)
               .AddCustomMarker("LiveKit w/o Avatar", withoutAvatarCount)
               .AddControl(new DebugConstLabelDef("Show Room Indicator"), new DebugToggleDef(debugAvatarsRooms));
        }

        public void Update()
        {
            var onLiveKit = 0;

            foreach (string walletId in entityParticipantTable.Wallets())
                if (roomHub.RoomsOf(walletId) != RoomSource.None)
                    onLiveKit++;

            var withoutAvatar = 0;

            foreach (string identity in roomHub.AllLocalRoomsRemoteParticipantIdentities())
                if (!entityParticipantTable.Has(identity))
                    withoutAvatar++;

            activeCount.SetAndUpdate(entityParticipantTable.Count.ToString());
            onLiveKitCount.SetAndUpdate(onLiveKit.ToString());
            offLiveKitCount.SetAndUpdate((entityParticipantTable.Count - onLiveKit).ToString());
            withoutAvatarCount.SetAndUpdate(withoutAvatar.ToString());
        }
    }
}
