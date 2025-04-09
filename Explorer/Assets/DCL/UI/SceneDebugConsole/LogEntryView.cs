using DCL.UI.SceneDebugConsole.LogHistory;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.SceneDebugConsole
{
    public class LogEntryView : MonoBehaviour
    {
        /*[SerializeField] private TMP_Text messageText;

        public void SetMessage(string message)
        {
            messageText.text = message;
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
        // [field: SerializeField] internal ChatEntryUsernameElement usernameElement { get; private set; }
        [field: SerializeField] internal LogEntryMessageBubbleElement messageBubbleElement { get; private set; }

        private SceneDebugConsoleLogMessage logMessage;

        private void Awake()
        {
            // profileButton.onClick.AddListener(OpenContextMenu);
        }

        private void OpenContextMenu()
        {
            // LogEntryClicked?.Invoke(profileButton.transform.position);
            LogEntryClicked?.Invoke();
        }

        public void AnimateLogEntry()
        {
            logEntryCanvasGroup.alpha = 0;
            logEntryCanvasGroup.DOFade(1, 0.5f);
        }

        public void SetItemData(SceneDebugConsoleLogMessage data)
        {
            logMessage = data;
            // usernameElement.SetUsername(data.SenderValidatedName, data.SenderWalletId);
            messageBubbleElement.SetMessageData(data);
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageBubbleElement.backgroundRectTransform.sizeDelta.y);
        }
    }
}
