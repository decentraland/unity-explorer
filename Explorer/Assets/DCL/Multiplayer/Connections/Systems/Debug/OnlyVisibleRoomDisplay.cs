using DCL.DebugUtilities.UIBindings;
using System;

namespace DCL.Multiplayer.Connections.Systems.Debug
{
    public class OnlyVisibleRoomDisplay : IRoomDisplay
    {
        private readonly IRoomDisplay origin;
        private readonly DebugWidgetVisibilityBinding visibilityBinding;

        public OnlyVisibleRoomDisplay(IRoomDisplay origin, DebugWidgetVisibilityBinding visibilityBinding)
        {
            this.origin = origin;
            this.visibilityBinding = visibilityBinding;
        }

        public void Update()
        {
            if (visibilityBinding.IsConnectedAndExpanded)
                origin.Update();
        }
    }
}
