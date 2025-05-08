using DCL.UI.SceneDebugConsole.LogHistory;
using MVC;
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

        [Tooltip("The time it waits, in seconds, since the scroll view reaches the bottom until the scroll-to-bottom button starts hiding.")]
        [SerializeField]
        private float scrollToBottomButtonTimeBeforeHiding = 2.0f;

        [Tooltip("The time it takes, in seconds, for the scroll-to-bottom button to fade out.")]
        [SerializeField]
        private float scrollToBottomButtonFadeOutDuration = 0.5f;

        [Header("UI elements")]
        [SerializeField]
        private TMP_InputField inputField;

        [SerializeField]
        private Button togglePanelButton;

        [SerializeField]
        private SceneDebugConsoleLogViewerElement logMessageViewer;

        [SerializeField]
        private Button clearButton;

        [SerializeField]
        private CanvasGroup consolePanelCanvasGroup;

        [SerializeField]
        private Button scrollToBottomButton;

        [SerializeField]
        private TMP_Text scrollToBottomNumberText;

        [SerializeField]
        private CanvasGroup scrollToBottomCanvasGroup;

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
        /// Gets or sets whether the console panel is open or close.
        /// </summary>
        public bool IsUnfolded
        {
            get => consolePanelCanvasGroup.alpha.Equals(1f);

            set
            {
                if (consolePanelCanvasGroup.alpha.Equals(value ? 1f : 0f))
                    return;

                consolePanelCanvasGroup.alpha = value ? 1f : 0f;
                consolePanelCanvasGroup.interactable = value;
                consolePanelCanvasGroup.blocksRaycasts = value;
                logMessageViewer.IsVisible = value;

                if (value)
                {
                    RefreshLogs();
                    ShowLatestLogs();
                }
                else
                {
                    inputField.DeactivateInputField();
                }

                FoldingChanged?.Invoke(value);
            }
        }

        public bool IsFocused { get; private set; }

        public void Dispose()
        {
            fadeoutCts.SafeCancelAndDispose();

            // inputField.onSelect.RemoveListener(OnInputFieldSelected);
            // inputField.onDeselect.RemoveListener(OnInputFieldDeselected);
            // inputField.onSubmit.RemoveListener(OnInputFieldSubmit);
            // clearButton.onClick.RemoveListener(OnClearButtonClicked);
            togglePanelButton.onClick.RemoveListener(OnTogglePanelButtonClicked);

            viewDependencies.DclInput.UI.Close.performed -= OnUIClosePerformed;
        }

        public void Initialize(IReadOnlyList<SceneDebugConsoleLogMessage> logMessages, SceneDebugConsoleSettings settings)
        {
            this.logMessages = logMessages;
            this.consoleSettings = settings;

            togglePanelButton.onClick.AddListener(OnTogglePanelButtonClicked);

            // clearButton.onClick.AddListener(OnClearButtonClicked);
            // inputField.onSelect.AddListener(OnInputFieldSelected);
            // inputField.onDeselect.AddListener(OnInputFieldDeselected);
            // inputField.onSubmit.AddListener(OnInputFieldSubmit);

            viewDependencies.DclInput.UI.Close.performed += OnUIClosePerformed;

            logMessageViewer.Initialize();
            logMessageViewer.SetData(logMessages);
        }

        private void OnUIClosePerformed(InputAction.CallbackContext callbackContext)
        {
            if (IsUnfolded)
                OnTogglePanelButtonClicked();
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

        public void RefreshLogs()
        {
            // UnityEngine.Debug.Log($"PRAVS - View.RefreshLogs()");
            logMessageViewer.RefreshLogs();

            // SetScrollToBottomVisibility(IsUnfolded && !IsScrollAtBottom && pendingMessages != 0, true);
        }

        /// <summary>
        /// Changes the visibility of the scroll-to-bottom button.
        /// </summary>
        /// <param name="isVisible">Whether to make it visible or invisible.</param>
        /// <param name="useAnimation">Whether to use a fading animation or change its visual state immediately.</param>
        /*public void SetScrollToBottomVisibility(bool isVisible, bool useAnimation = false)
        {
            // Resets animation
            scrollToBottomCanvasGroup.DOKill();

            if (isVisible)
            {
                scrollToBottomCanvasGroup.alpha = 1.0f;
                scrollToBottomButton.gameObject.SetActive(true);
            }
            else
            {
                if(useAnimation)
                    scrollToBottomCanvasGroup.DOFade(0.0f, scrollToBottomButtonFadeOutDuration).
                                              SetDelay(scrollToBottomButtonTimeBeforeHiding).
                                              OnComplete(() => { scrollToBottomButton.gameObject.SetActive(false); });
                else
                {
                    scrollToBottomCanvasGroup.alpha = 0.0f;
                    scrollToBottomButton.gameObject.SetActive(false);
                }
            }
        }*/

        /*private void AddLogEntryView(SceneDebugConsoleLogMessage logMessage)
        {
            // TODO: optimize with pool of this prefab
            GameObject entryGO = Instantiate(logEntryPrefab, logContentTransform);
            LogEntryView entryView = entryGO.GetComponent<LogEntryView>();
            entryView.SetItemData(logMessage);
            logEntryViews.Add(entryView);
        }*/

        /// <summary>
        /// Moves the console so it shows the latest logs.
        /// </summary>
        public void ShowLatestLogs()
        {
            /*Canvas.ForceUpdateCanvases();
            logScrollRect.normalizedPosition = new Vector2(0, 0);*/
            logMessageViewer.ShowLastMessage();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke();
            // panelBackgroundCanvasGroup.DOFade(1, BackgroundFadeTime);
            // chatMessageViewer.SetScrollbarVisibility(true, BackgroundFadeTime);
            // chatMessageViewer.StopChatEntriesFadeout();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke();
            // panelBackgroundCanvasGroup.DOFade(0, BackgroundFadeTime);
            // chatMessageViewer.SetScrollbarVisibility(false, BackgroundFadeTime);
            // chatMessageViewer.StartChatEntriesFadeout();
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            viewDependencies = dependencies;
            logMessageViewer.InjectDependencies(dependencies);
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

        private void OnTogglePanelButtonClicked()
        {
            IsUnfolded = !IsUnfolded;
        }
    }
}
