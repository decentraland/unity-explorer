using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Profiles.Tables;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class AvatarsRoomDisplay : IRoomDisplay
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ElementBinding<string> activeCount;

        internal readonly ElementBinding<bool> debugAvatarsRooms;

        public AvatarsRoomDisplay(IReadOnlyEntityParticipantTable entityParticipantTable, DebugWidgetBuilder widgetBuilder)
        {
            this.entityParticipantTable = entityParticipantTable;
            activeCount = new ElementBinding<string>(string.Empty);
            debugAvatarsRooms = new ElementBinding<bool>(false);

            widgetBuilder
               .AddCustomMarker("Active Avatars", activeCount)
               .AddControl(new DebugConstLabelDef("Show Room Indicator"), new DebugToggleDef(debugAvatarsRooms));
        }

        public void Update()
        {
            activeCount.SetAndUpdate(entityParticipantTable.Count.ToString());
        }
    }
}
