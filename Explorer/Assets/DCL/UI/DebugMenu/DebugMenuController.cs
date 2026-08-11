using DCL.Input;
using DCL.DebugUtilities;
using DCL.UI.DebugMenu.LogHistory;
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
        private AbConversionPanelView abConversionPanelView;

        private DebugPanelView visiblePanel;

        private IInputBlock inputBlock;

        private Button consoleButton;
        private Button abConversionButton;
        private Button debugPanelButton;

        private bool shouldRefreshConsole;
        private bool shouldHideDebugPanelOwnToggle;

        private IDebugContainerBuilder? debugContainerBuilder;

        private void OnEnable()
        {
            logsHistory.LogsUpdated += OnLogsUpdated;

            var root = GetComponent<UIDocument>().rootVisualElement;

            // Sidebar
            consoleButton = root.Q<Button>("ConsoleButton");
            abConversionButton = root.Q<Button>("AbConversionButton");
            debugPanelButton = root.Q<Button>("DebugPanelButton");

            consoleButton.clicked += OnConsoleButtonClicked;
            abConversionButton.clicked += OnAbConversionButtonClicked;

            // Views
            consolePanelView = new ConsolePanelView(root.Q("ConsolePanel"), consoleButton, OnConsoleButtonClicked, logsHistory);
            consolePanelView.SetInputBlock(inputBlock);
            abConversionPanelView = new AbConversionPanelView(root.Q("AbConversionPanel"), abConversionButton, OnAbConversionButtonClicked);

            // Shortcuts
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            // Live reload
            if (visiblePanel != null)
                switch (visiblePanel)
                {
                    // We will add other debug panel views here
                    case ConsolePanelView:
                        consolePanelView.Toggle();
                        visiblePanel = consolePanelView;
                        break;
                    case AbConversionPanelView:
                        abConversionPanelView.Toggle();
                        visiblePanel = abConversionPanelView;
                        break;
                }
        }

        public void Initialize(IInputBlock newInputBlock, IDebugContainerBuilder newBuilder)
        {
            SetInputBlock(newInputBlock);
            SetDebugContainerBuilder(newBuilder);
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

            // Cheap when nothing changed: a version check against the conversion metrics.
            abConversionPanelView?.Refresh();
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

        private void OnAbConversionButtonClicked() =>
            TogglePanel(abConversionPanelView);

        private void OnDebugPanelButtonClicked()
        {
            if (debugContainerBuilder == null) return;

            debugContainerBuilder.Container.TogglePanelVisibility();

            debugPanelButton.EnableInClassList(USS_SIDEBAR_BUTTON_SELECTED, debugContainerBuilder.Container.IsPanelVisible());
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
