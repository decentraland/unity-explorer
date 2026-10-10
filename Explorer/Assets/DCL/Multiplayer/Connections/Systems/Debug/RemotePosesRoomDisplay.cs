using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Profiles.Poses;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class RemotePosesRoomDisplay : IRoomDisplay
    {
        private readonly IRemoteMetadata remoteMetadata;
        private readonly ElementBinding<string> count;

        public RemotePosesRoomDisplay(IRemoteMetadata remoteMetadata, DebugWidgetBuilder widgetBuilder)
        {
            this.remoteMetadata = remoteMetadata;
            count = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .AddCustomMarker("Remote Metadata Count", count);
        }

        public void Update()
        {
            count.SetAndUpdate(remoteMetadata.Metadata.Count.ToString());
        }
    }
}
