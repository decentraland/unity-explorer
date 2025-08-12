using DCL.Input;
using DCL.Input.Component;
using DCL.UI.DebugMenu.LogHistory;
using DCL.UI.DebugMenu.UI.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.DebugMenu
{
    public class ConsolePanelView: DebugPanelView
    {
        private const string USS_COPY_TOAST_SHOW = "copy-success-toast--show";
        private const string USS_PAUSE_BUTTON_PLAY = "scene-debug-console__pause-button--play";
        private const long TOAST_DURATION = 1500L;

        private readonly Button pauseButton;
        private readonly Toggle showLogsToggle;
        private readonly Toggle showErrorsToggle;
        private readonly ListView consoleListView;
        private readonly ScrollView scrollView;
        private readonly TextField searchField;
        private readonly VisualElement copyToast;
        private readonly IVisualElementScheduledItem toastScheduledItem;

        private readonly DebugMenuConsoleLogHistory logsHistory;
        private IInputBlock inputBlock;

        public ConsolePanelView(VisualElement root, Button sidebarButton, Action closeClicked, DebugMenuConsoleLogHistory history) : base(root, sidebarButton, closeClicked)
        {
            logsHistory = history;

            var clearButton = root.Q<Button>("ClearButton");
            var copyAllButton = root.Q<Button>("CopyAllButton");
            pauseButton = root.Q<Button>("PauseButton");
            showLogsToggle = root.Q<Toggle>("LogsToggle");
            showErrorsToggle = root.Q<Toggle>("ErrorsToggle");
            consoleListView = root.Q<ListView>("ConsoleList");
            scrollView = consoleListView.Q<ScrollView>();
            searchField = root.Q<TextField>("FilterTextField");
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
            searchField.RegisterCallback<FocusInEvent, ConsolePanelView>(static (_, c) => c.inputBlock.Disable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER), this);
            searchField.RegisterCallback<FocusOutEvent, ConsolePanelView>(static (_, c) => c.inputBlock.Enable(InputMapComponent.Kind.SHORTCUTS, InputMapComponent.Kind.IN_WORLD_CAMERA, InputMapComponent.Kind.CAMERA, InputMapComponent.Kind.PLAYER), this);
        }

        public override void Toggle()
        {
            base.Toggle();

            if (Visible)
                Refresh(true);
        }

        public void Refresh(bool shouldScrollToBottom = false)
        {
            bool atBottom = IsScrollAtBottom();

            consoleListView.RefreshItems();

            if (shouldScrollToBottom || atBottom)
                consoleListView.ScrollToItem(consoleListView.itemsSource.Count - 1);

            showLogsToggle.text = $"LOGS ({logsHistory.LogEntryCount})";
            showErrorsToggle.text = $"ERRORS ({logsHistory.ErrorEntryCount})";
        }

        public void SetInputBlock(IInputBlock block)
        {
            this.inputBlock = block;
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

            pauseButton.EnableInClassList(USS_PAUSE_BUTTON_PLAY, logsHistory.Paused);
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

        // Cannot compare against 'highValue' directly due to floating point precision error
        private bool IsScrollAtBottom() =>
            scrollView != null
            && scrollView.verticalScroller.value >= (scrollView.verticalScroller.highValue * 0.999f);
    }
}
