using DG.Tweening;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Friends.UI.FriendPanel
{
    public enum FriendPanelStatus
    {
        ONLINE,
        OFFLINE,
        RECEIVED,
        SENT,
    }

    public class StatusWrapperView : MonoBehaviour
    {
        [field: SerializeField] public Button FolderButton { get; private set; }
        [field: SerializeField] public RectTransform FolderButtonGraphics { get; private set; }
        [field: SerializeField] public TMP_Text StatusText { get; private set; }

        [field: Space(10)]
        [field: SerializeField] public float FoldingAnimationDuration { get; private set; } = 0.3f;

        public Action<bool, FriendPanelStatus>? FolderButtonClicked;

        private bool isFolderOpen = true;
        private FriendPanelStatus panelStatus;

        private void Awake()
        {
            FolderButton.onClick.AddListener(FolderButtonClick);
        }

        public void SetStatusText(FriendPanelStatus status, int amount)
        {
            panelStatus = status;
            string statusText = status switch
            {
                FriendPanelStatus.ONLINE => "ONLINE",
                FriendPanelStatus.OFFLINE => "OFFLINE",
                FriendPanelStatus.RECEIVED => "RECEIVED",
                FriendPanelStatus.SENT => "SENT",
                _ => "Unknown"
            };

            StatusText.SetText($"{statusText} ({amount})");
        }

        private void FolderButtonClick()
        {
            isFolderOpen = !isFolderOpen;
            FolderButtonClicked?.Invoke(!isFolderOpen, panelStatus);
            SetFolderDirection(isFolderOpen);
        }

        private void SetFolderDirection(bool isOpen)
        {
            FolderButtonGraphics.DOScale(isOpen ? Vector3.one : new Vector3(1f, -1f, 1f), FoldingAnimationDuration);
        }
    }
}
