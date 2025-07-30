using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.Input.Component;
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
        private readonly IInputBlock inputBlock;

        private UIDocument uiDocument;
        private VisualElement uiDocumentRoot;
        private ListView consoleListView;
        private ScrollView scrollView;
        private Button clearButton;
        private bool isInputSelected;

        public SceneDebugConsoleController(SceneDebugConsoleLogEntryBus logEntriesBus, IInputBlock inputBlock)
        {
            this.inputBlock = inputBlock;
            this.logEntriesBus = logEntriesBus;
            this.logsHistory = new SceneDebugConsoleLogHistory();

            InstantiateRootGO();
        }

        // Instantiate root UI Document GameObject
        private void InstantiateRootGO()
        {
            logEntriesBus.MessageAdded += OnEntryAdded;
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            uiDocument = Object.Instantiate(Resources.Load<GameObject>("SceneDebugConsoleRootCanvas")).GetComponent<UIDocument>();
            uiDocumentRoot = uiDocument.rootVisualElement;
            uiDocumentRoot.visible = false;

            // ListView
            var logEntryUXML = Resources.Load<VisualTreeAsset>("SceneDebugConsoleLogEntry");
            consoleListView = uiDocumentRoot.Q<ListView>();
            consoleListView.makeItem = () => logEntryUXML.Instantiate();
            consoleListView.bindItem = (item, index) =>
            {
                var logEntry = logsHistory.FilteredLogMessages[index];

                bool isError = logEntry.Type == LogMessageType.Error;
                item.EnableInClassList("console__log-entry--error", isError);
                item.EnableInClassList("console__log-entry--log", !isError);

                item.Q<Label>().text = logEntry.Message;
            };

            // Set the actual item's source list/array
            consoleListView.itemsSource = logsHistory.FilteredLogMessages;

            consoleListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            scrollView = consoleListView.Q<ScrollView>();

            // Clear button
            clearButton = uiDocumentRoot.Q<Button>(name: "ClearButton");
            clearButton.clicked += ClearLogEntries;

            // Filter text field
            var textField = uiDocumentRoot.Q<TextField>(name: "FilterTextField");

            // React to text changes while typing
            textField.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                // Debug.Log($"Text changed from '{evt.previousValue}' to '{evt.newValue}'");
                if (string.IsNullOrEmpty(evt.newValue))
                    RemoveFilter();
                else
                    ApplyFilter(evt.newValue);
            });

            textField.RegisterCallback<FocusInEvent>((evt) => inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER));
            textField.RegisterCallback<FocusOutEvent>((evt) => inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER));

            // React to submission (Enter key press)
            // textField.RegisterCallback<NavigationSubmitEvent>((evt) =>
            // {
            //     Debug.Log($"PRAVS - Text submitted: {textField.value}");
            //     // ProcessUserInput(textField.value);
            //     ApplyFilter(textField.value);
            //
            //     // Stop event propagation to prevent other handlers
            //     evt.StopPropagation();
            // }, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            logEntriesBus.MessageAdded -= OnEntryAdded;
            clearButton.clicked -= ClearLogEntries;

            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
            // DCLInput.Instance.UI.Submit.performed -= OnSubmitShortcutPerformed;
        }

        private void ClearLogEntries()
        {
            logsHistory.ClearLogMessages();
            RefreshListViewAsync(IsScrollAtBottom()).Forget();
        }

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            uiDocumentRoot.visible = !uiDocumentRoot.visible;

            if (uiDocumentRoot.visible)
                RefreshListViewAsync(true).Forget();
        }

        private void OnEntryAdded(SceneDebugConsoleLogEntry entry)
        {
            logsHistory.AddLogMessage(entry);
            RefreshListViewAsync(IsScrollAtBottom()).Forget();
        }

        private void ApplyFilter(string targetText)
        {
            consoleListView.itemsSource = logsHistory.ApplyFilter(targetText);
            RefreshListViewAsync(true).Forget();
        }

        private void RemoveFilter()
        {
            consoleListView.itemsSource = logsHistory.RemoveFilters();
            RefreshListViewAsync(true).Forget();
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
