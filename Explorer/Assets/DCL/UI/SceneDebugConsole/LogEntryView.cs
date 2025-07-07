using DCL.UI.SceneDebugConsole.LogHistory;
using DG.Tweening;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    public class LogEntryView : MonoBehaviour
    {
        /*[SerializeField] private TMP_Text messageText;

        public void SetMessage(string entry)
        {
            messageText.text = entry;
        }

        public void SetTextColor(Color color)
        {
            messageText.color = color;
        }*/


        // public delegate void LogEntryClickedDelegate(Vector2 contextMenuPosition);
        public delegate void LogEntryClickedDelegate();

        public LogEntryClickedDelegate? LogEntryClicked;

        [field: SerializeField] internal RectTransform rectTransform { get; private set; }
        [field: SerializeField] internal CanvasGroup logEntryCanvasGroup { get; private set; }

        [field: Header("Elements")]
        [field: SerializeField] internal LogEntryMessageBubbleElement messageBubbleElement { get; private set; }

        private SceneDebugConsoleLogEntry logEntry;

        private void OpenContextMenu()
        {
            LogEntryClicked?.Invoke();
        }

        public void AnimateLogEntry()
        {
            logEntryCanvasGroup.alpha = 0;
            logEntryCanvasGroup.DOFade(1, 0.5f);
        }

        public void SetItemData(SceneDebugConsoleLogEntry data)
        {
            logEntry = data;
            messageBubbleElement.SetMessageData(data);
        }
    }
}
