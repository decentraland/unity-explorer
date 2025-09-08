using System;
using UnityEngine.UIElements;

namespace DCL.UI.ConnectionStatusPanel
{
    public class ConnectionPanelView
    {
        private const string USS_PANEL_HIDDEN = "connection-panel--hidden";

        public bool Visible { get; private set; }

        private readonly VisualElement panelRoot;
        private readonly ConnectionStatusElement sceneStatus;
        private readonly ConnectionStatusElement sceneRoomStatus;
        private readonly ConnectionStatusElement globalRoomStatus;

        public ConnectionPanelView(VisualElement panelRoot, Action closeClicked)
        {
            sceneStatus = panelRoot.Q<ConnectionStatusElement>("SceneStatus");
            sceneRoomStatus = panelRoot.Q<ConnectionStatusElement>("SceneRoomStatus");
            globalRoomStatus = panelRoot.Q<ConnectionStatusElement>("GlobalRoomStatus");

            this.panelRoot = panelRoot;

            panelRoot.EnableInClassList(USS_PANEL_HIDDEN, true);
            panelRoot.Q<Button>("CloseButton").clicked += closeClicked;
        }

        public virtual void Toggle()
        {
            Visible = !Visible;
            panelRoot.EnableInClassList(USS_PANEL_HIDDEN, Visible);
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
