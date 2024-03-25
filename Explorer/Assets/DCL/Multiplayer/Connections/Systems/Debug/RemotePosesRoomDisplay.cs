using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Profiles.Poses;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class RemotePosesRoomDisplay : IRoomDisplay
    {
        private readonly IRemotePoses remotePoses;
        private readonly ElementBinding<string> count;

        public RemotePosesRoomDisplay(IRemotePoses remotePoses, DebugWidgetBuilder widgetBuilder)
        {
            this.remotePoses = remotePoses;
            count = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .AddCustomMarker("Remote Poses Count", count);
        }

        public void Update()
        {
            count.SetAndUpdate(remotePoses.Count.ToString());
        }
    }
}
