using DCL.Chat.History;
using DCL.UI;
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

        public Action<GameObject> TooltipShown;

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
        private RectTransform claimedNameIcon;

        [SerializeField]
        private Image customIcon;

        [SerializeField]
        private RectTransform tooltipPosition;

        [SerializeField]
        private Image connectionStatusIndicator;

        [SerializeField]
        private OnlineStatusConfiguration onlineStatusConfiguration;

        public ChatChannel.ChannelId Id { get; set; }

        public event OpenButtonClickedDelegate OpenButtonClicked;
        public event RemoveButtonClickedDelegate RemoveButtonClicked;

        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            HideTooltip(false);
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

        public void SetConnectionStatus(OnlineStatus connectionStatus)
        {
            connectionStatusIndicator.color = onlineStatusConfiguration.GetConfiguration(connectionStatus).StatusColor;
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
            profilePictureView.gameObject.SetActive(true);
            profilePictureView.SetupWithDependencies(viewDependencies, userColor, faceSnapshotUrl, userId);
        }

        public void SetConversationIcon(Sprite icon)
        {
            customIcon.sprite = icon;
            customIcon.gameObject.SetActive(true);
            profilePictureView.gameObject.SetActive(false);
        }

        public void SetClaimedNameIconVisibility(bool isVisible)
        {
            claimedNameIcon.gameObject.SetActive(isVisible);
        }

        private void Awake()
        {
            openButton.onClick.AddListener(() => { OpenButtonClicked?.Invoke(this); });
            removeButton.onClick.AddListener(() => {
                HideTooltip(true);
                RemoveButtonClicked?.Invoke(this);
            });
            SetUnreadMessages(0);
        }

        private void Start()
        {
            tooltip.gameObject.SetActive(false);
        }

        public void ShowTooltip()
        {
            tooltip.gameObject.SetActive(true);
            tooltip.DOKill();
            tooltip.DOFade(1.0f, 0.3f).OnComplete(() => { TooltipShown?.Invoke(tooltip.gameObject); });
        }

        public void HideTooltip(bool isImmediate)
        {
            if (tooltip.gameObject.activeSelf)
            {
                if (isImmediate)
                {
                    tooltip.transform.SetParent(transform);
                    tooltip.transform.position = tooltipPosition.position;
                    tooltip.gameObject.SetActive(false);
                }
                else
                {
                    tooltip.DOKill();
                    tooltip.DOFade(0.0f, 0.3f).SetDelay(0.3f).OnComplete(() =>
                    {
                        tooltip.transform.SetParent(transform);
                        tooltip.transform.position = tooltipPosition.position;
                        tooltip.gameObject.SetActive(false);
                    });
                }
            }
        }
    }
}
