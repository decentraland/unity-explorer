using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Profiles.Tables;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class AvatarsRoomDisplay : IRoomDisplay
    {
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ElementBinding<string> activeCount;

        public AvatarsRoomDisplay(IReadOnlyEntityParticipantTable entityParticipantTable, DebugWidgetBuilder widgetBuilder)
        {
            this.entityParticipantTable = entityParticipantTable;
            activeCount = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .AddCustomMarker("Active Avatars", activeCount);
        }

        public AvatarsRoomDisplay(IReadOnlyEntityParticipantTable entityParticipantTable, ElementBinding<string> activeCount)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeCount = activeCount;
        }

        public void Update()
        {
            activeCount.SetAndUpdate(entityParticipantTable.Count.ToString());
        }
    }
}
