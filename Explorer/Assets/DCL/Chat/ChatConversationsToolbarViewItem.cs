using System.Threading;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DCL.UI;
using DCL.UI.Buttons;
using DCL.UI.ProfileElements;
using DCL.Utilities;
using DG.Tweening;
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

        [SerializeField]
        private ProfilePictureView profilePictureView;

        [SerializeField]
        protected GameObject thumbnailView;

        [SerializeField]
        private Button openButton;

        [SerializeField]
        private NumericBadgeUIElement unreadMessagesBadge;

        [SerializeField]
        private Image selectionMark;

        [SerializeField]
        protected Image customIcon;

        [Header("Connection status")]

        [SerializeField]
        private Image connectionStatusIndicator;

        [SerializeField]
        protected GameObject connectionStatusIndicatorContainer;

        [SerializeField]
        private OnlineStatusConfiguration onlineStatusConfiguration;

        [Header("Tooltip")]

        [SerializeField]
        private CanvasGroup tooltip;

        [SerializeField]
        protected TMP_Text tooltipText;

        [SerializeField]
        protected Button removeButton;

        [SerializeField]
        private RectTransform claimedNameIcon;

        [SerializeField]
        private RectTransform tooltipPosition;

        /// <summary>
        /// Gets or sets the identifier of the conversation.
        /// </summary>
        public ChatChannel.ChannelId Id { get; set; }

        /// <summary>
        /// Raised when the button to select / open the conversation is clicked.
        /// </summary>
        public event OpenButtonClickedDelegate OpenButtonClicked;

        /// <summary>
        /// Raised when the button to remove the conversation is clicked.
        /// </summary>
        public event RemoveButtonClickedDelegate RemoveButtonClicked;

        /// <summary>
        /// Raised when the tooltip of the icon appears.
        /// </summary>
        public event TooltipShownDelegate TooltipShown;

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

        /// <summary>
        /// Stores the name that will be shown in the tooltip.
        /// </summary>
        /// <param name="newName">The "name" of the conversation.</param>
        public void SetConversationName(string newName)
        {
            tooltipText.text = newName;
        }

        public void Configure(bool isClosable, bool hasOnlineStatus)
        {
            removeButton.gameObject.SetActive(isClosable);
            connectionStatusIndicatorContainer.SetActive(hasOnlineStatus);
        }

        /// <summary>
        /// Changes the color of the background and the letters of the tooltip.
        /// </summary>
        /// <param name="newName">The "name" of the conversation.</param>
        public virtual void SetColor(Color newColor)
        {
            tooltipText.color = newColor;
        }

        /// <summary>
        /// Replaces the value of unread messages to show next to the icon.
        /// </summary>
        /// <param name="currentUnreadMessages">The amount of unread messages in the conversation.</param>
        public void SetUnreadMessages(int currentUnreadMessages)
        {
            unreadMessagesBadge.Number = currentUnreadMessages;
        }

        /// <summary>
        /// Changes the visual aspect of the connection status indicator.
        /// </summary>
        /// <param name="connectionStatus">The current connection status.</param>
        public void SetConnectionStatus(OnlineStatus connectionStatus)
        {
            connectionStatusIndicator.color =
                onlineStatusConfiguration.GetConfiguration(connectionStatus).StatusColor;
        }

        /// <summary>
        /// Changes the visual aspect of the icon depending on whether it is selected or not.
        /// </summary>
        /// <param name="isSelected">Whether the conversation is selected.</param>
        public void SetSelectionStatus(bool isSelected)
        {
            selectionMark.gameObject.SetActive(isSelected);

            if(isSelected)
                openButton.OnSelect(null);
            else
                openButton.OnDeselect(null);
        }

        public void BindProfileThumbnail(IReactiveProperty<ProfileThumbnailViewModel.WithColor> viewModel)
        {
            customIcon.gameObject.SetActive(false);
            profilePictureView.gameObject.SetActive(true);
            profilePictureView.Bind(viewModel);
        }

        public void SetPicture(Sprite? sprite)
        {
            profilePictureView.gameObject.SetActive(true);
            customIcon.gameObject.SetActive(false);

            bool isLoading = sprite == null;

            profilePictureView.SetLoadingState(isLoading);

            if (!isLoading)
            {
                profilePictureView.SetImage(sprite);
            }
        }

        /// <summary>
        /// Replaces the profile picture with a custom icon.
        /// </summary>
        /// <param name="icon">The icon to show.</param>
        public void SetConversationIcon(Sprite icon)
        {
            customIcon.sprite = icon;
            customIcon.gameObject.SetActive(true);
            thumbnailView.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows or hides the "verified" icon.
        /// </summary>
        /// <param name="isVisible">Whether the icon has to be displayed or not.</param>
        public void SetClaimedNameIconVisibility(bool isVisible)
        {
            claimedNameIcon.gameObject.SetActive(isVisible);
        }

        /// <summary>
        /// Makes the tooltip of the item appear.
        /// </summary>
        public void ShowTooltip()
        {
            tooltip.gameObject.SetActive(true);
            tooltip.DOKill();
            tooltip.DOFade(1.0f, 0.3f).OnComplete(() => { TooltipShown?.Invoke(tooltip.gameObject); });
        }

        /// <summary>
        /// Hides the tooltip of the item with or without animations.
        /// </summary>
        /// <param name="isImmediate">Whether to skip animations or not.</param>
        public void HideTooltip(bool isImmediate)
        {
            if (tooltip.gameObject.activeSelf)
            {
                if (isImmediate)
                {
                    tooltip.transform.position = tooltipPosition.position;
                    tooltip.gameObject.SetActive(false);
                }
                else
                {
                    tooltip.DOKill();
                    tooltip.DOFade(0.0f, 0.3f).OnComplete(() =>
                    {
                        tooltip.transform.SetParent(transform);
                        tooltip.transform.position = tooltipPosition.position;
                        tooltip.gameObject.SetActive(false);
                    });
                }
            }
        }

        public void Initialize()
        {
            tooltip.gameObject.SetActive(false);
            removeButton.gameObject.SetActive(false);
            connectionStatusIndicatorContainer.SetActive(false);

            openButton.onClick.AddListener(() => { OpenButtonClicked?.Invoke(this); });
            removeButton.onClick.AddListener(() =>
            {
                HideTooltip(true);
                RemoveButtonClicked?.Invoke(this);
            });
            SetUnreadMessages(0);
        }

        protected virtual void Start()
        {

        }

        public void SetCommunityThumbnailData(ISpriteCache spriteCache, string communityImageUrl, CancellationToken none)
        {
        }
    }
}
