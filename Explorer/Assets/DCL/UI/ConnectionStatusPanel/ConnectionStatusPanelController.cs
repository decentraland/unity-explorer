using DCL.Ipfs;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.ConnectionStatusPanel
{
    [RequireComponent(typeof(UIDocument))]
    public class ConnectionStatusPanelController : MonoBehaviour
    {
        private const string USS_PANEL_HIDDEN = "connection-panel--hidden";

        private VisualElement rootContainer;
        private ConnectionPanelView panelView;

        // Gate visibility behind '/debug' chat command
        private bool isPanelEnabled;

        private void OnEnable()
        {
            rootContainer = GetComponent<UIDocument>().rootVisualElement;

            panelView = new ConnectionPanelView(rootContainer.Q("ConnectionPanel"), OnToggleButtonClicked);
            var toggleButton = rootContainer.Q<Button>("ConnectionButton");
            toggleButton.clicked += OnToggleButtonClicked;

            // We enable it only after the first Loading Screen is gone
            rootContainer.EnableInClassList(USS_PANEL_HIDDEN, true);
        }

        public void SetPanelEnabled(bool paneEnabled)
        {
            isPanelEnabled = paneEnabled;
            UpdateRootVisibility();
        }

        private void UpdateRootVisibility()
        {
            if (rootContainer == null) return;
            rootContainer.EnableInClassList(USS_PANEL_HIDDEN, !isPanelEnabled);
        }

        public void SetSceneStatus(ConnectionStatus status) =>
            panelView.SetSceneStatus(status);

        public void SetSceneRoomStatus(ConnectionStatus status) =>
            panelView.SetSceneRoomStatus(status);

        public void SetGlobalRoomStatus(ConnectionStatus status) =>
            panelView.SetGlobalRoomStatus(status);

        public void SetAssetBundleSceneStatus(AssetBundleRegistryEnum? status) =>
            panelView.SetAssetBundleSceneStatus(status);

        private void OnToggleButtonClicked() =>
            panelView.Toggle();
    }
}
