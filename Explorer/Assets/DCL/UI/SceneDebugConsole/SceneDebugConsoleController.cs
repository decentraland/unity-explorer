using Cysharp.Threading.Tasks;
using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using DCL.Input;
using DCL.Input.Component;
using MVC;
using System.Threading;
using UnityEngine.InputSystem;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleController : ControllerBase<SceneDebugConsoleView>
    {
        public delegate void ConsoleVisibilityChangedDelegate(bool isVisible);

        private readonly ISceneDebugConsoleMessageBus logMessagesBus;
        private readonly ISceneDebugConsoleLogHistory logHistory;
        private readonly IInputBlock inputBlock;
        private readonly ViewDependencies viewDependencies;
        private readonly ISceneDebugConsoleCommandsBus consoleCommandsBus;
        private readonly SceneDebugConsoleSettings consoleSettings;

        private bool isInputSelected;

        public override CanvasOrdering.SortingLayer Layer => CanvasOrdering.SortingLayer.Overlay;

        public bool IsUnfolded
        {
            get => viewInstance != null && viewInstance.IsUnfolded;
            set
            {
                if (viewInstance != null)
                {
                    viewInstance.IsUnfolded = value;

                    if (value)
                        viewInstance.ShowLatestLogs();

                    if (value)
                        viewDependencies.DclInput.UI.Submit.performed += OnSubmitShortcutPerformed;
                    else
                        viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
                }
            }
        }

        public event ConsoleVisibilityChangedDelegate ConsoleVisibilityChanged;

        public SceneDebugConsoleController(
            ViewFactoryMethod viewFactory,
            ISceneDebugConsoleMessageBus logMessagesBus,
            ISceneDebugConsoleLogHistory logHistory,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            ISceneDebugConsoleCommandsBus consoleCommandsBus,
            SceneDebugConsoleSettings consoleSettings) : base(viewFactory)
        {
            this.logMessagesBus = logMessagesBus;
            this.logHistory = logHistory;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.consoleCommandsBus = consoleCommandsBus;
            this.consoleSettings = consoleSettings;
        }

        public void Clear() // Called by a command
        {
            logHistory.ClearLogMessages();
            viewInstance?.RefreshLogs();
        }

        public override void Dispose()
        {
            logMessagesBus.MessageAdded -= OnMessageBusMessageAdded;
            logHistory.LogMessageAdded -= OnLogHistoryMessageAdded;
            consoleCommandsBus.OnClearConsole -= Clear;

            if (viewInstance != null)
            {
                viewInstance.PointerEnter -= OnConsoleViewPointerEnter;
                viewInstance.PointerExit -= OnConsoleViewPointerExit;
                viewInstance.InputBoxFocusChanged -= OnViewInputBoxFocusChanged;
                viewInstance.InputSubmitted -= OnViewInputSubmitted;
                viewInstance.FoldingChanged -= OnViewFoldingChanged;
                viewInstance.Dispose();
            }

            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            viewDependencies.DclInput.UI.Submit.performed -= OnSubmitShortcutPerformed;
        }

        protected override void OnViewInstantiated()
        {
            logMessagesBus.MessageAdded += OnMessageBusMessageAdded;
            logHistory.LogMessageAdded += OnLogHistoryMessageAdded;
            consoleCommandsBus.OnClearConsole += Clear;

            viewInstance!.InjectDependencies(viewDependencies);
            viewInstance!.Initialize(logHistory.LogMessages, consoleSettings);

            viewInstance.PointerEnter += OnConsoleViewPointerEnter;
            viewInstance.PointerExit += OnConsoleViewPointerExit;
            viewInstance.InputBoxFocusChanged += OnViewInputBoxFocusChanged;
            viewInstance.InputSubmitted += OnViewInputSubmitted;
            viewInstance.FoldingChanged += OnViewFoldingChanged;

            OnFocus();

            // Intro message
            logHistory.AddLogMessage(SceneDebugConsoleLogMessage.CommandResponse("Type 'help' for available commands."));
        }

        private void OnLogHistoryMessageAdded(SceneDebugConsoleLogMessage logMessage)
        {
            viewInstance?.RefreshLogs();

            if (consoleSettings.AutoScrollToBottom)
                viewInstance?.ShowLatestLogs();
        }

        private void OnViewFoldingChanged(bool isUnfolded)
        {
            ConsoleVisibilityChanged?.Invoke(isUnfolded);
        }

        protected override void OnBlur()
        {
            viewInstance!.DisableInputBoxSubmissions();
        }

        protected override void OnFocus()
        {
            if (viewInstance!.IsFocused) return;

            viewInstance.EnableInputBoxSubmissions();
        }

        protected override void OnViewShow()
        {
            base.OnViewShow();
            viewDependencies.DclInput.UI.Click.performed += OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            IsUnfolded = false; // Start hidden by default
        }

        protected override void OnViewClose()
        {
            base.OnViewClose();
            viewDependencies.DclInput.UI.Click.performed -= OnUIClickPerformed;
            viewDependencies.DclInput.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
        }

        protected override async UniTask WaitForCloseIntentAsync(CancellationToken ct)
        {
            await UniTask.Never(ct);
        }

        private void DisableUnwantedInputs()
        {
            inputBlock.Disable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private void EnableUnwantedInputs()
        {
            inputBlock.Enable(InputMapComponent.BLOCK_USER_INPUT);
        }

        private void OnViewInputBoxFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                DisableUnwantedInputs();
            else
                EnableUnwantedInputs();
        }

        private void OnConsoleViewPointerExit() => EnableUnwantedInputs();

        private void OnConsoleViewPointerEnter() => DisableUnwantedInputs();

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            IsUnfolded = !IsUnfolded;
        }

        private void OnUIClickPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.Click();
        }

        private void OnSubmitShortcutPerformed(InputAction.CallbackContext obj)
        {
            viewInstance!.FocusInputBox();
        }

        private void OnViewInputSubmitted(string commandText)
        {
            /*if (string.IsNullOrWhiteSpace(commandText))
                return;

            // Log the command
            logMessagesBus.SendCommand(commandText);

            // Execute the command and get the response
            string response = consoleCommandsBus.ExecuteCommand(commandText);

            if (!string.IsNullOrEmpty(response))
            {
                // Log the response
                logMessagesBus.SendCommandResponse(response);
            }*/
        }

        private void OnMessageBusMessageAdded(SceneDebugConsoleLogMessage message)
        {
            logHistory.AddLogMessage(message);
        }
    }
}
