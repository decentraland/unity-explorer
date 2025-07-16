using Cysharp.Threading.Tasks;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleController : IDisposable
    {
        private readonly SceneDebugConsoleLogEntryBus logEntriesBus;
        private readonly SceneDebugConsoleLogHistory logsHistory;

        private UIDocument uiDocument;
        private VisualElement uiDocumentRoot;
        private ListView consoleListView;
        private ScrollView scrollView;
        private bool isInputSelected;

        public SceneDebugConsoleController(
            SceneDebugConsoleLogEntryBus logEntriesBus,
            SceneDebugConsoleLogHistory logsHistory)
        {
            this.logEntriesBus = logEntriesBus;
            this.logsHistory = logsHistory;

            InstantiateRootGO();
        }

        // Instantiate root UI Document GameObject
        private void InstantiateRootGO()
        {
            logEntriesBus.MessageAdded += OnEntryBusEntryAdded;
            logsHistory.LogMessageAdded += OnLogsHistoryEntryAdded;
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            uiDocument = Object.Instantiate(Resources.Load<GameObject>("SceneDebugConsoleRootCanvas")).GetComponent<UIDocument>();
            uiDocumentRoot = uiDocument.rootVisualElement;
            uiDocumentRoot.visible = false;

            var logEntryUXML = Resources.Load<VisualTreeAsset>("SceneDebugConsoleLogEntry");
            consoleListView = uiDocumentRoot.Q<ListView>();
            consoleListView.makeItem = () => logEntryUXML.Instantiate();
            consoleListView.bindItem = (item, index) =>
            {
                var logEntry = logsHistory.LogMessages[index];

                bool isError = logEntry.Type == LogMessageType.Error;
                item.EnableInClassList("console__log-entry--error", isError);
                item.EnableInClassList("console__log-entry--log", !isError);

                item.Q<Label>().text = logEntry.Message;
            };

            // Set the actual item's source list/array
            consoleListView.itemsSource = logsHistory.LogMessages;

            consoleListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            scrollView = consoleListView.Q<ScrollView>();
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            logEntriesBus.MessageAdded -= OnEntryBusEntryAdded;
            logsHistory.LogMessageAdded -= OnLogsHistoryEntryAdded;

            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            // DCLInput.Instance.UI.Submit.performed -= OnSubmitShortcutPerformed;
        }

        private void Clear()
        {
            logsHistory.ClearLogMessages();
        }

        private void OnLogsHistoryEntryAdded(SceneDebugConsoleLogEntry logEntry)
        {
            Debug.Log($"PRAVS - Controller.OnLogsHistoryEntryAdded({logEntry.Message})");
            RefreshListViewAsync(IsScrollAtBottom()).Forget();
        }

        /*private void OnViewFoldingChanged(bool isUnfolded)
        {
            ConsoleVisibilityChanged?.Invoke(isUnfolded);
        }*/

        /*private void DisableUnwantedInputs()
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
        }*/

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            uiDocumentRoot.visible = !uiDocumentRoot.visible;
        }

        // TODO: IS THE LOG MESSAGEBUS + HISTORY NEEDED? CAN WE HAVE ONLY 1 BUS ??
        private void OnEntryBusEntryAdded(SceneDebugConsoleLogEntry entry)
        {
            logsHistory.AddLogMessage(entry);
        }

        // It can only be refreshed on the MAIN THREAD, otherwise it doesn't work and fails silently...
        // TODO: Find out if we can instantiate the 'SceneDebugConsoleController' on the main thread instead of this...
        private async UniTask RefreshListViewAsync(bool scrollToBottom)
        {
            await UniTask.SwitchToMainThread();
            consoleListView.RefreshItems();

            if (scrollToBottom)
                consoleListView.ScrollToItem(consoleListView.itemsSource.Count-1);
        }

        // Cannot compare against 'highValue' directly due to floating point precision error
        private bool IsScrollAtBottom() =>
            scrollView != null
            && scrollView.verticalScroller.value >= (scrollView.verticalScroller.highValue * 0.999f);
    }
}
