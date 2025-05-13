using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ORIGINAL OPEN SOURCE PLUGIN SCRIPT:
// https://github.com/stillwwater/command_terminal/blob/0f5918ea79014955b24c7d431625a97a1cac8797/CommandTerminal/Terminal.cs

namespace CommandTerminal
{
    /// <summary>
    ///     Dedicated toggleable console for displaying scene messages/logs/errors during Local Scene Development
    /// </summary>
    public class SceneDebugConsole : MonoBehaviour
    {
        private enum TerminalState
        {
            Close,
            OpenSmall,
            OpenFull,
        }

        private readonly struct LogItem
        {
            public readonly LogType Type;
            public readonly string Message;
            public readonly string StackTrace;

            public LogItem(LogType type, string message, string stackTrace)
            {
                Type = type;
                Message = message;
                StackTrace = stackTrace;
            }
        }

        [Header("Window")]
        [SerializeField]
        private Button toggleConsoleButton;
        [Range(0, 1)]
        [SerializeField]
        private float maxHeight = 0.7f;

        [SerializeField]
        [Range(0, 1)]
        private float smallTerminalRatio = 0.4f;

        [Range(100, 1000)]
        [SerializeField]
        private float toggleSpeed = 1000;

        [SerializeField] private InputActionAsset inputActions;
        private InputAction toggleInputAction;
        private InputAction toggleLargeInputAction;
        [SerializeField] private int logsMaxAmount = 512;
        [SerializeField] private bool autoScrollOnNewLogs = false;

        [Header("Theme")]
        [SerializeField] private int fontSize = 20;
        [SerializeField] private Font consoleFont;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color foregroundColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;

        private Queue<LogItem> logs = new ();
        private Queue<LogItem> logsToBeProcessed = new Queue<LogItem>();
        private Texture2D backgroundTex;
        private float currentOpenValue;
        private TextEditor editorState;
        private GUIStyle labelStyle;
        private float openTarget;
        private float realWindowSize;
        private Vector2 scrollPosition;
        private TerminalState state;
        private Rect screenRect;
        private GUIStyle windowStyle;
        private bool isScrollAtBottom = false;
        private float normalizedScrollPosition = 1f;
        private float totalContentHeight = 0f;
        float scrollYCompensation = 0;

        private bool isClosed => state == TerminalState.Close && Mathf.Approximately(currentOpenValue, openTarget);

        // Log() is called from a thread (many GUI operations have to run in the main thread)
        public void Log(string message, string stackTrace = "", LogType logType = LogType.Log)
        {
            logsToBeProcessed.Enqueue(new LogItem(logType, message, stackTrace));
        }

        private void Start()
        {
            var shortcutsInputActionsMap = inputActions.FindActionMap("Shortcuts");
            toggleInputAction = shortcutsInputActionsMap.FindAction("ToggleSceneDebugConsole");
            toggleInputAction.Enable();
            toggleLargeInputAction = shortcutsInputActionsMap.FindAction("ToggleSceneDebugConsoleLarger");
            toggleLargeInputAction.Enable();

            SetupWindow();
            SetupLabels();
        }

        private void OnEnable()
        {
            toggleConsoleButton.onClick.AddListener(OnToggleConsoleButtonClicked);
        }

        private void OnDisable()
        {
            toggleConsoleButton.onClick.RemoveListener(OnToggleConsoleButtonClicked);
        }

        private void OnToggleConsoleButtonClicked()
            => SetState(isClosed ? TerminalState.OpenSmall : TerminalState.Close);

        private void Update()
        {
            if (isClosed)
            {
                if (toggleLargeInputAction.triggered)
                    SetState(TerminalState.OpenFull);
                else if (toggleInputAction.triggered)
                    SetState(TerminalState.OpenSmall);

                return;
            }

            if (toggleInputAction.triggered || toggleLargeInputAction.triggered)
                SetState(TerminalState.Close);
        }

        private void OnGUI()
        {
            if (isClosed) return;

            HandleOpenness();
            screenRect = GUILayout.Window(88, screenRect, WindowUpdate, "", windowStyle);

            // Calculate normalized scroll position
            float viewportHeight = screenRect.height - windowStyle.padding.vertical;
            float maxScrollValue = Mathf.Max(0, totalContentHeight - viewportHeight);
            normalizedScrollPosition = maxScrollValue > 0 ? scrollPosition.y / maxScrollValue : 1f;

            isScrollAtBottom = normalizedScrollPosition >= 0.99f;
        }

        private void SetState(TerminalState newState)
        {
            switch (newState)
            {
                case TerminalState.Close:
                {
                    openTarget = 0;
                    break;
                }
                case TerminalState.OpenSmall:
                {
                    openTarget = Screen.height * maxHeight * smallTerminalRatio;

                    if (currentOpenValue > openTarget)
                    {
                        // Prevent resizing from OpenFull to OpenSmall if window y position
                        // is greater than OpenSmall's target
                        openTarget = 0;
                        state = TerminalState.Close;
                        return;
                    }

                    realWindowSize = openTarget;
                    scrollPosition.y = int.MaxValue;
                    isScrollAtBottom = true;
                    break;
                }
                case TerminalState.OpenFull:
                default:
                {
                    realWindowSize = Screen.height * maxHeight;
                    openTarget = realWindowSize;
                    scrollPosition.y = int.MaxValue;
                    isScrollAtBottom = true;
                    break;
                }
            }

            state = newState;
        }

        private void SetupWindow()
        {
            realWindowSize = Screen.height * maxHeight / 3;
            screenRect = new Rect(0, currentOpenValue - realWindowSize, Screen.width, realWindowSize);

            backgroundTex = new Texture2D(1, 1);
            backgroundTex.SetPixel(0, 0, backgroundColor);
            backgroundTex.Apply();

            windowStyle = new GUIStyle();
            windowStyle.normal.background = backgroundTex;
            windowStyle.padding = new RectOffset(4, 4, 4, 4);
            windowStyle.normal.textColor = foregroundColor;
            windowStyle.font = consoleFont;
        }

        private void SetupLabels()
        {
            labelStyle = new GUIStyle();
            labelStyle.font = consoleFont;
            labelStyle.normal.textColor = foregroundColor;
            labelStyle.wordWrap = true;
            labelStyle.alignment = TextAnchor.LowerLeft;
            labelStyle.fontSize = fontSize;
        }

        private void WindowUpdate(int Window2D)
        {
            // Process logs in main thread, before drawing
            while (logsToBeProcessed.Count > 0)
            {
                HandleLog(logsToBeProcessed.Dequeue());
                if (autoScrollOnNewLogs)
                    scrollPosition.y = int.MaxValue;

                UpdateScrollPosition();
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUIStyle.none);

            DrawLogs();

            GUILayout.EndScrollView();
        }

        private void HandleLog(LogItem newLog)
        {
            // Calculate and update content height
            logs.Enqueue(newLog);
            float newLogHeight = CalculateLogLabelHeight(newLog.Message);
            totalContentHeight += newLogHeight;

            if (logs.Count > logsMaxAmount)
            {
                LogItem removedLog = logs.Dequeue(); // remove oldest
                float removedLogHeight = CalculateLogLabelHeight(removedLog.Message);
                totalContentHeight -= removedLogHeight;

                if (!autoScrollOnNewLogs)
                    scrollYCompensation += newLogHeight;
            }
        }

        private void UpdateScrollPosition()
        {
            // If scroll is at the bottom, keep it at the bottom
            if (isScrollAtBottom)
            {
                scrollPosition.y = int.MaxValue;
                scrollYCompensation = 0;
                return;
            }

            if (scrollYCompensation > 0)
            {
                scrollPosition.y = Mathf.Max(0, scrollPosition.y - scrollYCompensation);
                scrollYCompensation = 0;
                return;
            }
        }

        private float CalculateLogLabelHeight(string message)
        {
            var content = new GUIContent(message);
            float contentWidth = screenRect.width - windowStyle.padding.horizontal;
            float height = labelStyle.CalcHeight(content, contentWidth);
            return height;
        }

        private void DrawLogs()
        {
            GUILayout.BeginVertical();
            foreach (LogItem log in logs)
            {
                labelStyle.normal.textColor = GetLogColor(log.Type);
                GUILayout.Label(log.Message, labelStyle);
            }
            GUILayout.EndVertical();
        }

        private void HandleOpenness()
        {
            float dt = toggleSpeed * Time.unscaledDeltaTime;

            if (currentOpenValue < openTarget)
            {
                currentOpenValue += dt;
                if (currentOpenValue > openTarget) currentOpenValue = openTarget;
            }
            else if (currentOpenValue > openTarget)
            {
                currentOpenValue -= dt;
                if (currentOpenValue < openTarget) currentOpenValue = openTarget;
            }
            else
            {
                return; // Already at target
            }

            screenRect = new Rect(0, currentOpenValue - realWindowSize, Screen.width, realWindowSize);
        }

        private Color GetLogColor(LogType type)
        {
            switch (type)
            {
                case LogType.Log: return foregroundColor;
                case LogType.Warning: return warningColor;
                default: return errorColor;
            }
        }
    }
}
