using Cysharp.Threading.Tasks;
using DCL.UI.SceneDebugConsole.LogHistory;
using MVC;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Utility;

namespace DCL.UI.SceneDebugConsole
{
    public class SceneDebugConsoleView : ViewBase, IView, IViewWithGlobalDependencies, IPointerEnterHandler, IPointerExitHandler, IDisposable
    {
        public delegate void FoldingChangedDelegate(bool isUnfolded);
        public delegate void InputBoxFocusChangedDelegate(bool hasFocus);
        public delegate void InputSubmittedDelegate(string command);
        public delegate void PointerEventDelegate();

        [Header("Settings")]
        [Tooltip("The time it takes, in seconds, for the background of the console window to fade-in/out when hovering with the mouse.")]
        [SerializeField]
        private float backgroundFadeTime = 0.2f;

        [Header("UI elements")]
        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private CanvasGroup panelBackgroundCanvasGroup;

        [SerializeField]
        private Button closeConsoleButton;

        [SerializeField]
        private ScrollRect logScrollRect;

        [SerializeField]
        private RectTransform logContentTransform;

        [SerializeField]
        private GameObject logEntryPrefab;

        [SerializeField]
        private Button clearButton;

        [SerializeField]
        private GameObject consolePanel;

        /// <summary>
        /// Raised when the mouse pointer hovers any part of the console window.
        /// </summary>
        public event PointerEventDelegate PointerEnter;

        /// <summary>
        /// Raised when the mouse pointer stops hovering the console window.
        /// </summary>
        public event PointerEventDelegate PointerExit;

        /// <summary>
        /// Raised when either the input box gains the focus or loses it.
        /// </summary>
        public event InputBoxFocusChangedDelegate InputBoxFocusChanged;

        /// <summary>
        /// Raised whenever the user attempts to send the content of the input box as a command.
        /// </summary>
        public event InputSubmittedDelegate InputSubmitted;

        /// <summary>
        /// Raised when the UI is folded or unfolded.
        /// </summary>
        public event FoldingChangedDelegate FoldingChanged;

        private ViewDependencies viewDependencies;
        private CancellationTokenSource fadeoutCts;

        private bool isInputSelected;
        private IReadOnlyList<SceneDebugConsoleLogMessage> logMessages;
        private SceneDebugConsoleSettings consoleSettings;
        private readonly List<LogEntryView> logEntryViews = new();

        /// <summary>
        /// Get or sets the current content of the input field.
        /// </summary>
        public string InputFieldText
        {
            get => inputField.text;
            set => inputField.text = value;
        }

        /// <summary>
        /// Gets whether the scroll view is showing the bottom of the content, and it can't scroll down anymore.
        /// </summary>
        public bool IsScrollAtBottom => Mathf.Approximately(logScrollRect.normalizedPosition.y, 0f);

        /// <summary>
        /// Gets or sets whether the console panel is open or close.
        /// </summary>
        public bool IsUnfolded
        {
            get => consolePanel.activeSelf;

            set
            {
                if (value == consolePanel.activeSelf)
                    return;

                consolePanel.SetActive(value);

                if (!value)
                {
                    inputField.DeactivateInputField();
                }

                FoldingChanged?.Invoke(value);
            }
        }

        public bool IsFocused { get; private set; }

        private void Start()
        {
            panelBackgroundCanvasGroup.alpha = 0;
        }

        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();

            inputField.onSelect.RemoveListener(OnInputFieldSelected);
            inputField.onDeselect.RemoveListener(OnInputFieldDeselected);
            inputField.onSubmit.RemoveListener(OnInputFieldSubmit);
            clearButton.onClick.RemoveListener(OnClearButtonClicked);
            closeConsoleButton.onClick.RemoveListener(OnCloseConsoleButtonClicked);

            viewDependencies.DclInput.UI.Close.performed -= OnUIClosePerformed;
        }

        public void Initialize(IReadOnlyList<SceneDebugConsoleLogMessage> logMessages, SceneDebugConsoleSettings settings)
        {
            this.logMessages = logMessages;
            this.consoleSettings = settings;

            closeConsoleButton.onClick.AddListener(OnCloseConsoleButtonClicked);
            clearButton.onClick.AddListener(OnClearButtonClicked);

            inputField.onSelect.AddListener(OnInputFieldSelected);
            inputField.onDeselect.AddListener(OnInputFieldDeselected);
            inputField.onSubmit.AddListener(OnInputFieldSubmit);

            viewDependencies.DclInput.UI.Close.performed += OnUIClosePerformed;

            RefreshLogs();
        }

        private void OnUIClosePerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsUnfolded)
                OnCloseConsoleButtonClicked();
        }

        /// <summary>
        /// Makes the input field stop receiving user inputs.
        /// </summary>
        public void DisableInputBoxSubmissions()
        {
            IsFocused = false;
            inputField.interactable = false;
        }

        /// <summary>
        /// Makes the input field start receiving user inputs.
        /// </summary>
        public void EnableInputBoxSubmissions()
        {
            IsFocused = true;
            inputField.interactable = true;
        }

        /// <summary>
        /// Makes the input field gain the focus. It does not modify its content.
        /// </summary>
        public void FocusInputBox()
        {
            if (gameObject.activeInHierarchy)
            {
                inputField.ActivateInputField();
                inputField.Select();
            }
        }

        /// <summary>
        /// Makes sure the console window is showing all the log messages in the history.
        /// </summary>
        public void RefreshLogs()
        {
            // Clean up existing entries
            foreach (var entry in logEntryViews)
            {
                Destroy(entry.gameObject);
            }
            logEntryViews.Clear();

            // Create new entries for all log messages
            foreach (var logMessage in logMessages)
            {
                AddLogEntryView(logMessage);
            }
        }

        private void AddLogEntryView(SceneDebugConsoleLogMessage logMessage)
        {
            GameObject entryGO = Instantiate(logEntryPrefab, logContentTransform);
            LogEntryView entryView = entryGO.GetComponent<LogEntryView>();

            if (entryView != null)
            {
                string timestampPrefix = consoleSettings.ShowTimestamps ? $"[{logMessage.Timestamp:HH:mm:ss}] " : "";
                entryView.SetMessage($"{timestampPrefix}{logMessage.Message}");

                // Set color based on message type
                switch (logMessage.Type)
                {
                    case LogMessageType.Log:
                        entryView.SetTextColor(consoleSettings.LogColor);
                        break;
                    case LogMessageType.Warning:
                        entryView.SetTextColor(consoleSettings.WarningColor);
                        break;
                    case LogMessageType.Error:
                        entryView.SetTextColor(consoleSettings.ErrorColor);
                        break;
                    case LogMessageType.Command:
                        entryView.SetTextColor(consoleSettings.CommandColor);
                        break;
                    case LogMessageType.CommandResponse:
                        entryView.SetTextColor(consoleSettings.CommandResponseColor);
                        break;
                }

                logEntryViews.Add(entryView);
            }
        }

        /// <summary>
        /// Performs a click event on the console window.
        /// </summary>
        public void Click()
        {
            // No action needed for click in this simplified version
        }

        /// <summary>
        /// Moves the console so it shows the latest logs.
        /// </summary>
        public void ShowLatestLogs()
        {
            Canvas.ForceUpdateCanvases();
            logScrollRect.normalizedPosition = new Vector2(0, 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
            panelBackgroundCanvasGroup.DOFade(1, backgroundFadeTime);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
            panelBackgroundCanvasGroup.DOFade(0, backgroundFadeTime);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
        }

        private void OnInputFieldSelected(string value)
        {
            isInputSelected = true;
            InputBoxFocusChanged?.Invoke(true);
        }

        private void OnInputFieldDeselected(string value)
        {
            isInputSelected = false;
            InputBoxFocusChanged?.Invoke(false);
        }

        private void OnInputFieldSubmit(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                InputSubmitted?.Invoke(value);
                inputField.text = string.Empty;

                // Re-focus the input field after submitting
                FocusInputBox();
            }
        }

        private void OnClearButtonClicked()
        {
            foreach (var entry in logEntryViews)
            {
                Destroy(entry.gameObject);
            }
            logEntryViews.Clear();
        }

        private void OnCloseConsoleButtonClicked()
        {
            IsUnfolded = false;
        }
    }

    // Simple component for log entry views
    public class LogEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;

        public void SetMessage(string message)
        {
            messageText.text = message;
        }

        public void SetTextColor(Color color)
        {
            messageText.color = color;
        }
    }
}
