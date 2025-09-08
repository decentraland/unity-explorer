using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.ConnectionStatusPanel
{
    [RequireComponent(typeof(UIDocument))]
    public class ConnectionStatusPanelGOController : MonoBehaviour
    {
        private ConnectionPanelView panelView;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            panelView = new ConnectionPanelView(root.Q("ConnectionPanel"), OnToggleButtonClicked);
            var toggleButton = root.Q<Button>("ConnectionButton");
            toggleButton.clicked += OnToggleButtonClicked;
        }

        public void SetSceneStatus(ConnectionStatus status) =>
            panelView.SetSceneStatus(status);

        public void SetSceneRoomStatus(ConnectionStatus status) =>
            panelView.SetSceneRoomStatus(status);

        public void SetGlobalRoomStatus(ConnectionStatus status) =>
            panelView.SetGlobalRoomStatus(status);

        private void OnToggleButtonClicked() =>
            panelView.Toggle();
    }
}
