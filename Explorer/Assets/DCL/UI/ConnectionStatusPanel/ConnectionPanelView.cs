using DCL.Ipfs;
using System;
using UnityEngine.UIElements;

namespace DCL.UI.ConnectionStatusPanel
{
    public class ConnectionPanelView
    {
        private const string USS_PANEL_HIDDEN = "connection-panel--hidden";

        private bool visible = false;
        private bool shownOnce;

        private readonly VisualElement panelRoot;
        private readonly ConnectionStatusElement sceneStatus;
        private readonly ConnectionStatusElement sceneRoomStatus;
        private readonly ConnectionStatusElement globalRoomStatus;
        private readonly AssetBundleSceneStatusElement assetBundleSceneStatus;

        public ConnectionPanelView(VisualElement panelRoot, Action closeClicked)
        {
            sceneStatus = panelRoot.Q<ConnectionStatusElement>("SceneStatus");
            sceneRoomStatus = panelRoot.Q<ConnectionStatusElement>("SceneRoomStatus");
            globalRoomStatus = panelRoot.Q<ConnectionStatusElement>("GlobalRoomStatus");
            assetBundleSceneStatus = panelRoot.Q<AssetBundleSceneStatusElement>("AssetBundleStatus");

            this.panelRoot = panelRoot;

            panelRoot.EnableInClassList(USS_PANEL_HIDDEN, !visible);
            panelRoot.Q<Button>("CloseButton").clicked += closeClicked;

            assetBundleSceneStatus.EnableInClassList(USS_PANEL_HIDDEN, true);
        }

        public virtual void Toggle()
        {
            visible = !visible;

            if (!shownOnce && visible)
            {
                // We use this (plus setting display to None in OnEnable) to force UI Toolkit
                // to redraw all the items on the first open. Without it some styles are not applied.
                panelRoot.style.display = DisplayStyle.Flex;
                shownOnce = true;
            }

            panelRoot.EnableInClassList(USS_PANEL_HIDDEN, !visible);
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

        public void SetAssetBundleSceneStatus(AssetBundleRegistryEnum? status)
        {
            assetBundleSceneStatus.EnableInClassList(USS_PANEL_HIDDEN, !status.HasValue);
            if (status.HasValue)
                assetBundleSceneStatus.SetStatus(status.Value);
        }
    }
}
