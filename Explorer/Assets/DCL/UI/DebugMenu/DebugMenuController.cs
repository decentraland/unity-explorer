using DCL.Input;
using DCL.UI.DebugMenu.LogHistory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class DebugMenuController : MonoBehaviour
    {
        private readonly DebugMenuConsoleLogHistory logsHistory = new ();

        private ConsolePanelView consolePanelView;
        private SettingsPanelView settingsPanelView;
        private ConnectionPanelView connectionPanelView;

        private DebugPanelView visiblePanel;

        private IInputBlock inputBlock;

        private Button consoleButton;
        private Button settingsButton;
        private Button connectionButton;

        private bool shouldRefreshConsole;

        private void OnEnable()
        {
            logsHistory.LogsUpdated += OnLogsUpdated;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // TODO: Check chat: ChatCommandsBus.Instance.ConnectionStatusPanelVisibilityChanged -= VisibilityChanged;

            // Sidebar
            consoleButton = root.Q<Button>("ConsoleButton");
            settingsButton = root.Q<Button>("DebugButton");
            connectionButton = root.Q<Button>("ConnectionButton");

            consoleButton.clicked += OnConsoleButtonClicked;
            settingsButton.clicked += OnSettingsButtonClicked;
            connectionButton.clicked += OnConnectionButtonClicked;

            // Views
            consolePanelView = new ConsolePanelView(root.Q("ConsolePanel"), consoleButton, OnConsoleButtonClicked, logsHistory);
            consolePanelView.SetInputBlock(inputBlock);
            settingsPanelView = new SettingsPanelView(root.Q("SettingsPanel"), settingsButton, OnSettingsButtonClicked);
            connectionPanelView = new ConnectionPanelView(root.Q("ConnectionPanel"), connectionButton, OnConnectionButtonClicked);

            // Shortcuts
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            // Live reload
            if (visiblePanel != null)
                switch (visiblePanel)
                {
                    case ConsolePanelView:
                        consolePanelView.Toggle();
                        visiblePanel = consolePanelView;
                        break;
                    case SettingsPanelView:
                        settingsPanelView.Toggle();
                        visiblePanel = settingsPanelView;
                        break;
                    case ConnectionPanelView:
                        connectionPanelView.Toggle();
                        visiblePanel = connectionPanelView;
                        break;
                }
        }

        public void SetInputBlock(IInputBlock block)
        {
            this.inputBlock = block;
            consolePanelView.SetInputBlock(block);
        }

        public void SetSceneStatus(ConnectionStatus status) =>
            connectionPanelView.SetSceneStatus(status);

        public void SetSceneRoomStatus(ConnectionStatus status) =>
            connectionPanelView.SetSceneRoomStatus(status);

        public void SetGlobalRoomStatus(ConnectionStatus status) =>
            connectionPanelView.SetGlobalRoomStatus(status);

        private void OnDisable()
        {
            logsHistory.LogsUpdated -= OnLogsUpdated;
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
        }

        private void Update()
        {
            if (shouldRefreshConsole)
            {
                shouldRefreshConsole = false;
                consolePanelView.Refresh();
            }
        }

        public void PushLog(DebugMenuConsoleLogEntry logEntry)
        {
            logsHistory.AddLogMessage(logEntry);
        }

        private void OnConsoleButtonClicked() =>
            TogglePanel(consolePanelView);

        private void OnSettingsButtonClicked() =>
            TogglePanel(settingsPanelView);

        private void OnConnectionButtonClicked() =>
            TogglePanel(connectionPanelView);

        private void TogglePanel(DebugPanelView panelView)
        {
            if (panelView.Visible)
            {
                panelView.Toggle();
                visiblePanel = null;
            }
            else
            {
                visiblePanel?.Toggle();
                panelView.Toggle();
                visiblePanel = panelView;
            }
        }

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj) =>
            TogglePanel(consolePanelView);

        private void OnLogsUpdated()
        {
            if (!consolePanelView.Visible) return;
            shouldRefreshConsole = true;
        }
    }
}
