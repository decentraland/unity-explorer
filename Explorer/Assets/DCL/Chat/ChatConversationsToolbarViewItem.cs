using DCL.Chat.History;
using DCL.UI.Buttons;
using DCL.UI.ProfileElements;
using DG.Tweening;
using MVC;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.Chat
{
    public class ChatConversationsToolbarViewItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public delegate void OpenButtonClickedDelegate(ChatConversationsToolbarViewItem item);
        public delegate void RemoveButtonClickedDelegate(ChatConversationsToolbarViewItem item);

        [SerializeField]
        private ProfilePictureView profilePictureView;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private CanvasGroup tooltip;

        [SerializeField]
        private TMP_Text tooltipText;

        [SerializeField]
        private Button removeButton;

        [SerializeField]
        private NumericBadgeUIElement unreadMessagesBadge;

        [SerializeField]
        private Image selectionMark;

        [SerializeField]
        private Image customIcon;

        public ChatChannel.ChannelId Id { get; set; }

        public event OpenButtonClickedDelegate OpenButtonClicked;
        public event RemoveButtonClickedDelegate RemoveButtonClicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip.gameObject.SetActive(true);
            tooltip.DOKill();
            tooltip.DOFade(1.0f, 0.5f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip.DOKill();
            tooltip.DOFade(0.0f, 0.5f).OnComplete(() => { tooltip.gameObject.SetActive(false); });
        }

        public void SetConversationName(string newName)
        {
            tooltipText.text = newName;
        }

        public void SetConversationType(bool isPrivate)
        {
            removeButton.gameObject.SetActive(isPrivate);
            // TODO: hide connection icon
        }

        public void SetUnreadMessages(int currentUnreadMessages)
        {
            unreadMessagesBadge.Number = currentUnreadMessages;
        }

        public void SetConnectionStatus(bool isConnected)
        {
            // TODO
        }

        public void SetSelectionStatus(bool isSelected)
        {
            selectionMark.gameObject.SetActive(isSelected);

            if(isSelected)
                openButton.OnSelect(null);
            else
                openButton.OnDeselect(null);
        }

        public void SetProfileData(ViewDependencies viewDependencies, Color userColor, string faceSnapshotUrl, string userId)
        {
            customIcon.gameObject.SetActive(false);
            profilePictureView.gameObject.SetActive(false);
            profilePictureView.SetupWithDependencies(viewDependencies, userColor, faceSnapshotUrl, userId);
        }

        private void Awake()
        {
            openButton.onClick.AddListener(() => { OpenButtonClicked?.Invoke(this); });
            removeButton.onClick.AddListener(() => { RemoveButtonClicked?.Invoke(this); });
            SetUnreadMessages(0);
        }

        public void SetConversationIcon(Sprite icon)
        {
            customIcon.sprite = icon;
            customIcon.gameObject.SetActive(true);
            profilePictureView.gameObject.SetActive(false);
        }
    }
}
