using CDPBridges;
using DCL.Input;
using DCL.DebugUtilities;
using DCL.UI.DebugMenu.LogHistory;
using DCL.WebRequests.ChromeDevtool;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    [RequireComponent(typeof(UIDocument))]
    public class DebugMenuController : MonoBehaviour
    {
        private const string USS_SIDEBAR_BUTTON_SELECTED = "sidebar__button--selected";
        private readonly DebugMenuConsoleLogHistory logsHistory = new ();

        private ConsolePanelView consolePanelView;
        private ConnectionPanelView connectionPanelView;

        private DebugPanelView visiblePanel;

        private IInputBlock inputBlock;
        private ChromeDevtoolProtocolClient chromeDevtoolProtocolClient;

        private Button consoleButton;
        private Button debugPanelButton;
        private Button connectionButton;
        private Button chromeDevtoolsProtocolButton;

        private bool shouldRefreshConsole;
        private bool shouldHideDebugPanelOwnToggle;

        private IDebugContainerBuilder? debugContainerBuilder;

        private void OnEnable()
        {
            logsHistory.LogsUpdated += OnLogsUpdated;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // Sidebar
            consoleButton = root.Q<Button>("ConsoleButton");
            connectionButton = root.Q<Button>("ConnectionButton");
            debugPanelButton = root.Q<Button>("DebugPanelButton");
            chromeDevtoolsProtocolButton = root.Q<Button>("ChromeDevtoolsProtocolButton");

            consoleButton.clicked += OnConsoleButtonClicked;
            connectionButton.clicked += OnConnectionButtonClicked;
            chromeDevtoolsProtocolButton.clicked += OnChromeDevtoolsProtocolButton;

            // Views
            consolePanelView = new ConsolePanelView(root.Q("ConsolePanel"), consoleButton, OnConsoleButtonClicked, logsHistory);
            consolePanelView.SetInputBlock(inputBlock);
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
                    case ConnectionPanelView:
                        connectionPanelView.Toggle();
                        visiblePanel = connectionPanelView;
                        break;
                }
        }

        public void Initialize(IInputBlock newInputBlock, IDebugContainerBuilder newBuilder, ChromeDevtoolProtocolClient newChromeDevtoolProtocolClient)
        {
            SetInputBlock(newInputBlock);
            SetDebugContainerBuilder(newBuilder);
            chromeDevtoolProtocolClient = newChromeDevtoolProtocolClient;
        }

        private void SetDebugContainerBuilder(IDebugContainerBuilder builder)
        {
            debugContainerBuilder = builder;

            // Panel handled at DebugContainerBuilder
            debugPanelButton.clicked -= OnDebugPanelButtonClicked;
            debugPanelButton.clicked += OnDebugPanelButtonClicked;
            debugPanelButton.style.display = DisplayStyle.Flex;

            // DebugPanel has its own separate toggle button (that must still be used when the
            // DebugMenu is not enabled), so we must hide that one.
            shouldHideDebugPanelOwnToggle = true;
        }

        private void SetInputBlock(IInputBlock block)
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
            if (shouldHideDebugPanelOwnToggle)
            {
                // Hide DebugPanel own toggle button when DebugMenu is available
                // Cannot be done at SetDebugContainerBuilder() due to Container being built
                // only AFTER all the Plugins are initialized...
                HideDebugPanelOwnToggle();
            }

            if (shouldRefreshConsole)
            {
                shouldRefreshConsole = false;
                consolePanelView.Refresh();
            }
        }

        private void HideDebugPanelOwnToggle()
        {
            try
            {
                // DebugContainerBuilder may throw InvalidOperationException during initialization
                debugContainerBuilder?.Container.HideToggleButton();
                shouldHideDebugPanelOwnToggle = false;
            }
            catch (Exception)
            {
                // If Container hasn't been built yet, it will be retried on the next frame because
                // shouldHideDebugPanelOwnToggle doesn't get reset when that happens
            }
        }

        public void PushLog(DebugMenuConsoleLogEntry logEntry)
        {
            logsHistory.AddLogMessage(logEntry);
        }

        private void OnConsoleButtonClicked() =>
            TogglePanel(consolePanelView);

        private void OnDebugPanelButtonClicked()
        {
            if (debugContainerBuilder == null) return;

            debugContainerBuilder.Container.TogglePanelVisibility();

            debugPanelButton.EnableInClassList(USS_SIDEBAR_BUTTON_SELECTED, debugContainerBuilder.Container.IsPanelVisible());
        }

        private void OnConnectionButtonClicked() =>
            TogglePanel(connectionPanelView);

        private void OnChromeDevtoolsProtocolButton()
        {
            BridgeStartResult result = chromeDevtoolProtocolClient!.Start();
            //TODO show toast if error
        }

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
