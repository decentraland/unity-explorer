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
        public delegate void TooltipShownDelegate(GameObject tooltip);

        public event TooltipShownDelegate TooltipShown;

        [SerializeField]
        private ProfilePictureView profilePictureView;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private NumericBadgeUIElement unreadMessagesBadge;

        [SerializeField]
        private Image selectionMark;

        [SerializeField]
        private Image customIcon;

        [Header("Connection status")]

        [SerializeField]
        private Image connectionStatusIndicator;

        [SerializeField]
        private GameObject connectionStatusIndicatorContainer;

        [SerializeField]
        private OnlineStatusConfiguration onlineStatusConfiguration;

        [Header("Tooltip")]

        [SerializeField]
        private CanvasGroup tooltip;

        [SerializeField]
        private TMP_Text tooltipText;

        [SerializeField]
        private Button removeButton;

        [SerializeField]
        private RectTransform claimedNameIcon;

        [SerializeField]
        private RectTransform tooltipPosition;

        public ChatChannel.ChannelId Id { get; set; }

        public event OpenButtonClickedDelegate OpenButtonClicked;
        public event RemoveButtonClickedDelegate RemoveButtonClicked;

        // Also called by the component in the tooltip
        public void OnPointerEnter(PointerEventData eventData)
        {
            ShowTooltip();
        }

        // Also called by the component in the tooltip
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

        public void SetConnectionStatusVisibility(bool isVisible)
        {
            connectionStatusIndicatorContainer.SetActive(isVisible);
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
    }
}
