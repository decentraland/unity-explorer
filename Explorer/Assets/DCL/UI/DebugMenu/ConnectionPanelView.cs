using DCL.UI.DebugMenu.UI.Elements;
using System;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    public class ConnectionPanelView: DebugPanelView
    {
        private readonly ConnectionStatusElement sceneStatus;
        private readonly ConnectionStatusElement sceneRoomStatus;
        private readonly ConnectionStatusElement globalRoomStatus;

        public ConnectionPanelView(VisualElement root, Button sidebarButton, Action closeClicked) : base(root, sidebarButton, closeClicked)
        {
            sceneStatus = root.Q<ConnectionStatusElement>("SceneStatus");
            sceneRoomStatus = root.Q<ConnectionStatusElement>("SceneRoomStatus");
            globalRoomStatus = root.Q<ConnectionStatusElement>("GlobalRoomStatus");
        }

        public void SetSceneStatus(ConnectionStatus status)
        {
            sceneStatus.SetStatus(status);
        }

        public void SetSceneRoomStatus(ConnectionStatus status)
        {
            sceneRoomStatus.SetStatus(status);
        }

        public void SetGlobalRoomStatus(ConnectionStatus status)
        {
            globalRoomStatus.SetStatus(status);
        }
    }
}
