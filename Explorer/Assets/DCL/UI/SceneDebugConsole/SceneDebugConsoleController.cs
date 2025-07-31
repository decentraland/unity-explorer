using DCL.Input;
using DCL.Input.Component;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.UI.Elements;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace DCL.UI.SceneDebugConsole
{
    [RequireComponent(typeof(UIDocument))]
    public class SceneDebugConsoleController : MonoBehaviour
    {
        private const string USS_COPY_TOAST_SHOW = "copy-success-toast--show";
        private const string USS_CONSOLE_HIDDEN = "scene-debug-console--hidden";
        private const long TOAST_DURATION = 1500L;

        [SerializeField] private int maxLogMessages = 1500; // TODO
        [SerializeField] private bool showTimestamps = true; // TODO

        private readonly SceneDebugConsoleLogHistory logsHistory = new ();

        private IInputBlock inputBlock;

        private VisualElement consoleWindow;
        private Button pauseButton;
        private ListView consoleListView;
        private ScrollView scrollView;
        private TextField searchField;
        private Toggle showLogsToggle;
        private Toggle showErrorsToggle;
        private VisualElement copyToast;
        private IVisualElementScheduledItem toastScheduledItem;

        private bool isHidden = true;
        private bool shownOnce;
        private bool shouldRefresh;
        private bool shouldBottomOnRefresh;

        public void SetInputBlock(IInputBlock block)
        {
            // InputBlock should really be a singleton :(
            this.inputBlock = block;
        }

        private void Start()
        {
            logsHistory.LogsUpdated += OnLogsUpdated;
        }

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed += OnToggleConsoleShortcutPerformed;

            // Log callbacks
            logsHistory.LogsUpdated += OnLogsUpdated;

            consoleWindow = root.Q("ConsoleWindow");
            consoleWindow.EnableInClassList(USS_CONSOLE_HIDDEN, isHidden);
            consoleWindow.style.display = isHidden ? DisplayStyle.None : DisplayStyle.Flex;

            var clearButton = root.Q<Button>("ClearButton");
            var copyAllButton = root.Q<Button>("CopyAllButton");
            pauseButton = root.Q<Button>("PauseButton");
            consoleListView = root.Q<ListView>("ConsoleList");
            scrollView = consoleListView.Q<ScrollView>();
            searchField = root.Q<TextField>("FilterTextField");
            showLogsToggle = root.Q<Toggle>("LogsToggle");
            showErrorsToggle = root.Q<Toggle>("ErrorsToggle");
            copyToast = root.Q("CopyToast");
            toastScheduledItem = copyToast.schedule.Execute(() => copyToast.RemoveFromClassList(USS_COPY_TOAST_SHOW));

            // Setup ListView
            consoleListView.itemsSource = logsHistory.FilteredLogMessages;
            consoleListView.makeItem = () => new ConsoleEntryElement();

            consoleListView.bindItem = (item, index) =>
            {
                var ve = (ConsoleEntryElement)item;
                var logEntry = logsHistory.FilteredLogMessages[index];

                ve.SetData(logEntry.Type, logEntry.Message);
            };

            consoleListView.selectedIndicesChanged += OnConsoleSelectionChanged;

            // Buttons / Toggles
            clearButton.clicked += OnClearClicked;
            pauseButton.clicked += OnPauseClicked;
            copyAllButton.clicked += OnCopyAllClicked;
            searchField.RegisterValueChangedCallback(_ => RefreshFilters());
            showLogsToggle.RegisterValueChangedCallback(_ => RefreshFilters());
            showErrorsToggle.RegisterValueChangedCallback(_ => RefreshFilters());

            // Input blocking
            searchField.RegisterCallback<FocusInEvent, SceneDebugConsoleController>(static (_, c) => c.inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER), this);
            searchField.RegisterCallback<FocusOutEvent, SceneDebugConsoleController>(static (_, c) => c.inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER), this);

            if (!isHidden) shouldRefresh = true;
        }

        public void OnDisable()
        {
            DCLInput.Instance.Shortcuts.ToggleSceneDebugConsole.performed -= OnToggleConsoleShortcutPerformed;
        }

        public void Update()
        {
            if (!shouldRefresh) return;
            shouldRefresh = false;

            consoleListView.RefreshItems();

            if (shouldBottomOnRefresh)
                consoleListView.ScrollToItem(consoleListView.itemsSource.Count-1);
            shouldBottomOnRefresh = false;

            showLogsToggle.text = $"LOGS ({logsHistory.LogEntryCount})";
            showErrorsToggle.text = $"ERRORS ({logsHistory.ErrorEntryCount})";
        }

        public void PushLog(SceneDebugConsoleLogEntry logEntry)
        {
            logsHistory.AddLogMessage(logEntry);
        }

        private void RefreshFilters()
        {
            logsHistory.ApplyFilter(searchField.value, showErrorsToggle.value, showLogsToggle.value);
        }

        private void OnClearClicked()
        {
            logsHistory.ClearLogMessages();
        }

        private void OnPauseClicked()
        {
            logsHistory.Paused = !logsHistory.Paused;

            // TODO: Set icon to pause / play
            // pauseButton.iconImage = ?
        }

        private void OnCopyAllClicked()
        {
            if (logsHistory.FilteredLogMessages.Count == 0)
                return;

            var allMessages = new System.Text.StringBuilder();

            foreach (var logEntry in logsHistory.FilteredLogMessages)
                allMessages.AppendLine(logEntry.ToString());

            CopyString(allMessages.ToString());
        }

        private void OnConsoleSelectionChanged(IEnumerable<int> selection)
        {
            if (consoleListView.selectedIndex == -1) return;

            var selectedEntry = logsHistory.FilteredLogMessages[consoleListView.selectedIndex];
            CopyString(selectedEntry.ToString());

            consoleListView.SetSelectionWithoutNotify(Enumerable.Empty<int>());
        }

        private void CopyString(string text)
        {
            GUIUtility.systemCopyBuffer = text;

            copyToast.AddToClassList(USS_COPY_TOAST_SHOW);
            toastScheduledItem.ExecuteLater(TOAST_DURATION);
        }

        private void OnToggleConsoleShortcutPerformed(InputAction.CallbackContext obj)
        {
            if (!shownOnce)
            {
                // We use this (plus setting display to None in OnEnable) to force UI Toolkit
                // to redraw all the items on the first open. Without it some styles are not applied.
                consoleWindow.style.display = DisplayStyle.Flex;
                shownOnce = true;
            }

            isHidden = !isHidden;

            consoleWindow.EnableInClassList(USS_CONSOLE_HIDDEN, isHidden);

            if (isHidden) return;

            shouldRefresh = true;
            shouldBottomOnRefresh = true;
        }

        private void OnLogsUpdated()
        {
            if (isHidden) return;
            shouldRefresh = true;
            shouldBottomOnRefresh = IsScrollAtBottom();
        }

        // Cannot compare against 'highValue' directly due to floating point precision error
        private bool IsScrollAtBottom() =>
            scrollView != null
            && scrollView.verticalScroller.value >= (scrollView.verticalScroller.highValue * 0.999f);
    }
}
