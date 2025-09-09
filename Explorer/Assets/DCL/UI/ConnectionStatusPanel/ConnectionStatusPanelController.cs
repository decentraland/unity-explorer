using DCL.AuthenticationScreenFlow;
using DCL.ExplorePanel;
using DCL.Ipfs;
using DCL.SceneLoadingScreens;
using MVC;
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
        private IMVCManager? uGUIMVCManager;

        private void OnEnable()
        {
            rootContainer = GetComponent<UIDocument>().rootVisualElement;

            panelView = new ConnectionPanelView(rootContainer.Q("ConnectionPanel"), OnToggleButtonClicked);
            var toggleButton = rootContainer.Q<Button>("ConnectionButton");
            toggleButton.clicked += OnToggleButtonClicked;

            // We enable it only after the first Loading Screen is gone
            rootContainer.EnableInClassList(USS_PANEL_HIDDEN, true);
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

        // We need to hide the whole Connection Status Panel (and its toggle button) during
        // other (UGUI) fullscreen panels because UiToolkit elements cannot be sorted against UGUI ones...
        public void SetUGUIMVCManager(IMVCManager mvcManager)
        {
            if (uGUIMVCManager != null) return;
            uGUIMVCManager = mvcManager;
            uGUIMVCManager.OnViewShowed += OnMvcManagerViewShowed;
            uGUIMVCManager.OnViewClosed += OnMvcManagerViewClosed;
        }

        private void OnMvcManagerViewShowed(IController showedController)
        {
            if (showedController is not SceneLoadingScreenController
                && showedController is not AuthenticationScreenController
                && showedController is not ExplorePanelController) return;

            rootContainer.EnableInClassList(USS_PANEL_HIDDEN, true);
        }

        private void OnMvcManagerViewClosed(IController closedController)
        {
            if (closedController is not SceneLoadingScreenController
                && closedController is not AuthenticationScreenController
                && closedController is not ExplorePanelController) return;

            rootContainer.EnableInClassList(USS_PANEL_HIDDEN, false);
        }
    }
}
