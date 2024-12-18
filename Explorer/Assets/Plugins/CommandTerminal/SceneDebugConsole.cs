using UnityEngine;
using UnityEngine.InputSystem;

// ORIGINAL OPEN SOURCE PLUGIN SCRIPT:
// https://github.com/stillwwater/command_terminal/blob/0f5918ea79014955b24c7d431625a97a1cac8797/CommandTerminal/Terminal.cs

namespace CommandTerminal
{
    public enum TerminalState
    {
        Close,
        OpenSmall,
        OpenFull,
    }

    /// <summary>
    ///     Dedicated toggleable console for displaying only JS scene messages/logs/errors
    /// </summary>
    public class SceneDebugConsole : MonoBehaviour
    {
        [Header("Window")]
        [Range(0, 1)]
        [SerializeField]
        private float maxHeight = 0.7f;

        [SerializeField]
        [Range(0, 1)]
        private float smallTerminalRatio = 0.33f;

        [Range(100, 1000)]
        [SerializeField]
        private float toggleSpeed = 1000;

        [SerializeField] private InputActionAsset inputActions;
        private InputAction toggleInputAction;
        private InputAction toggleLargeInputAction;
        [SerializeField] private int logsMaxAmount = 512;

        [Header("Theme")]
        [SerializeField] private int fontSize = 20;
        [SerializeField] private Font consoleFont;
        [SerializeField] private Color backgroundColor = Color.black;
        [SerializeField] private Color foregroundColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        private Texture2D backgroundTex;
        private ConsoleBuffer consoleBuffer;
        private float currentOpenValue;
        private TextEditor editorState;
        private GUIStyle labelStyle;
        private float openTarget;
        private float realWindowSize;
        private Vector2 scrollPosition;
        private TerminalState state;
        private Rect window;
        private GUIStyle windowStyle;

        private bool isClosed => state == TerminalState.Close && Mathf.Approximately(currentOpenValue, openTarget);

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
            consoleBuffer = new ConsoleBuffer(logsMaxAmount);
        }

        private void Update()
        {
            if (isClosed)
            {
                if (toggleLargeInputAction.triggered)
                    SetState(TerminalState.OpenFull);
                else if (toggleInputAction.triggered)
                    SetState(TerminalState.OpenSmall);
            }
            else if (toggleInputAction.triggered || toggleLargeInputAction.triggered)
            {
                SetState(TerminalState.Close);
            }
        }

        private void OnGUI()
        {
            if (isClosed) return;

            HandleOpenness();
            window = GUILayout.Window(88, window, DrawConsole, "", windowStyle);
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
                    break;
                }
                case TerminalState.OpenFull:
                default:
                {
                    realWindowSize = Screen.height * maxHeight;
                    openTarget = realWindowSize;
                    break;
                }
            }

            state = newState;
        }

        private void SetupWindow()
        {
            realWindowSize = Screen.height * maxHeight / 3;
            window = new Rect(0, currentOpenValue - realWindowSize, Screen.width, realWindowSize);

            // Set background color
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
            labelStyle.alignment = TextAnchor.LowerRight;
            labelStyle.fontSize = fontSize;
        }

        private void DrawConsole(int Window2D)
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, false, GUIStyle.none, GUIStyle.none);
            GUILayout.FlexibleSpace();
            DrawLogs();
            GUILayout.EndScrollView();
        }

        private void DrawLogs()
        {
            foreach (LogItem log in consoleBuffer.Logs)
            {
                labelStyle.normal.textColor = GetLogColor(log.Type);
                GUILayout.Label(log.Message, labelStyle);
            }
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

            window = new Rect(0, currentOpenValue - realWindowSize, Screen.width, realWindowSize);
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

        public void Log(string message, string stackTrace = "", LogType type = LogType.Log, bool scrollToBottom = true)
        {
            consoleBuffer.HandleLog(message, stackTrace, type);

            if (scrollToBottom)
                scrollPosition.y = int.MaxValue;
        }
    }
}
