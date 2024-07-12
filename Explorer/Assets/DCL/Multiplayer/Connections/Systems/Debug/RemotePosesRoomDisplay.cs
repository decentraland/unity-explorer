using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Multiplayer.Profiles.Poses;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class RemotePosesRoomDisplay : IRoomDisplay
    {
        private readonly IRemotePoses remotePoses;
        private readonly ElementBinding<string> count;
        private readonly DebugWidgetVisibilityBinding visibilityBinding = new (false);

        public RemotePosesRoomDisplay(IRemotePoses remotePoses, DebugWidgetBuilder widgetBuilder)
        {
            this.remotePoses = remotePoses;
            count = new ElementBinding<string>(string.Empty);

            widgetBuilder
               .SetVisibilityBinding(visibilityBinding)
               .AddCustomMarker("Remote Poses Count", count);
        }

        public void Update()
        {
            if (visibilityBinding.IsExpanded)
                count.SetAndUpdate(remotePoses.Count.ToString());
        }
    }
}
