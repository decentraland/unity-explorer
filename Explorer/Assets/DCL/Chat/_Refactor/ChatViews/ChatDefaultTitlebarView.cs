using DCL.Chat.ChatViewModels;
using DCL.UI;
using DCL.UI.ProfileElements;
using DCL.VoiceChat;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Chat.ChatViews
{
    public class ChatDefaultTitlebarView : MonoBehaviour
    {
        // Thumbnail opacity in the titlebar should never be != 0 as per design
        private const float THUMBNAIL_GREY_OUT_OPACITY = 0f;

        public event Action? OnCloseRequested;
        public event Action? OnMembersRequested;
        public event Action? OnContextMenuRequested;
        public event Action<TitlebarViewMode>? OnProfileContextMenuRequested;

        public Button ButtonClose => buttonClose;
        public Button ButtonOpenMembers => buttonOpenMembers;
        public Button ButtonOpenContextMenu => buttonOpenContextMenu;
        public Button ButtonOpenProfileContextMenu => buttonOpenProfileContextMenu;
        public CallButtonView ButtonStartCall => buttonStartCall;

        private Image profileCtxMenuButtonImage => (Image)buttonOpenProfileContextMenu.targetGraphic;

        [SerializeField] private Button buttonClose = null!;
        [SerializeField] private Button buttonOpenMembers = null!;
        [SerializeField] private Button buttonOpenContextMenu = null!;
        [SerializeField] private Button buttonOpenProfileContextMenu = null!;

        [SerializeField] private CallButtonView buttonStartCall = null!;

        [SerializeField] private TMP_Text textChannelName = null!;
        [SerializeField] private TMP_Text textMembersCount = null!;
        [SerializeField] private ChatProfileView chatProfileView = null!;
        [SerializeField] private GameObject nearbyElementsContainer = null!;
        [SerializeField] private GameObject nearbyAutoTranslateIndicator = null!;
        [SerializeField] private SkeletonLoadingView loadingView = null!;

        [Space(10)]
        [SerializeField] private float communityGraphicsPixelMultiplier = 3.0f;
        [SerializeField] private float dmGraphicsPixelMultiplier = 1.5f;

        private TitlebarViewMode currentViewMode;
        private ColorBlock profileCtxMenuButtonNormalColors;
        private ColorBlock profileCtxMenuButtonOpenColors;

        [SerializeField]
        private Image connectionStatusIndicator = null!;

        private void Awake()
        {
            buttonOpenContextMenu.onClick.AddListener(() => OnContextMenuRequested?.Invoke());
            buttonOpenProfileContextMenu.onClick.AddListener(() => OnProfileContextMenuRequested?.Invoke(currentViewMode));
            buttonClose.onClick.AddListener(() => OnCloseRequested?.Invoke());
            buttonOpenMembers.onClick.AddListener(() => OnMembersRequested?.Invoke());

            profileCtxMenuButtonNormalColors = buttonOpenProfileContextMenu.colors;
            profileCtxMenuButtonOpenColors = buttonOpenProfileContextMenu.colors;
            profileCtxMenuButtonOpenColors.normalColor = profileCtxMenuButtonOpenColors.highlightedColor;
        }

        public void StartLoading() =>
            loadingView.ShowLoading();

        public void StopLoading() =>
            loadingView.HideLoading();

        public void SetContextMenuButtonSelectedAppearance() =>
            buttonOpenProfileContextMenu.colors = profileCtxMenuButtonOpenColors;

        public void SetContextMenuButtonNormalAppearance() =>
            buttonOpenProfileContextMenu.colors = profileCtxMenuButtonNormalColors;

        public void Setup(ChatTitlebarViewModel model)
        {
            currentViewMode = model.ViewMode;
            textChannelName.text = model.Username;

            bool shouldShowMembersButton = model.ViewMode == TitlebarViewMode.Nearby ||
                                           model.ViewMode == TitlebarViewMode.Community;

            buttonOpenMembers.gameObject.SetActive(shouldShowMembersButton);

            bool isUnresolvedPlaceholder = string.IsNullOrEmpty(model.Id)
                                           && model.Thumbnail.Value.ThumbnailState is ProfileThumbnailViewModel.State.Loading
                                                                                       or ProfileThumbnailViewModel.State.NotBound;

            if (isUnresolvedPlaceholder)
            {
                chatProfileView.gameObject.SetActive(false);
                nearbyElementsContainer.SetActive(false);
                connectionStatusIndicator.gameObject.SetActive(false);
                if (model.ViewMode == TitlebarViewMode.DirectMessage)
                    buttonOpenMembers.gameObject.SetActive(false);
                return;
            }

            bool showProfile = model.ViewMode == TitlebarViewMode.DirectMessage ||
                               model.ViewMode == TitlebarViewMode.Community;

            chatProfileView.gameObject.SetActive(showProfile);
            nearbyElementsContainer.SetActive(model.ViewMode == TitlebarViewMode.Nearby);

            if (showProfile)
                chatProfileView.Setup(model);

            buttonOpenProfileContextMenu.interactable = model.ViewMode is TitlebarViewMode.Community or TitlebarViewMode.DirectMessage;
            profileCtxMenuButtonImage.pixelsPerUnitMultiplier = model.ViewMode == TitlebarViewMode.Community ? communityGraphicsPixelMultiplier : dmGraphicsPixelMultiplier;

            if (model.ViewMode == TitlebarViewMode.DirectMessage)
            {
                SetConnectionStatus(model.IsOnline);
            }
            else
            {
                SetConnectionStatus(true);
                connectionStatusIndicator.gameObject.SetActive(false);
            }
        }

        public void SetAutoTranslateIndicatorForNearby(bool isVisible)
        {
            if (nearbyAutoTranslateIndicator != null)
                nearbyAutoTranslateIndicator.SetActive(isVisible);
        }

        private void SetConnectionStatus(bool isOnline)
        {
            connectionStatusIndicator.gameObject.SetActive(isOnline);
            if (chatProfileView != null)
                chatProfileView.SetConnectionStatus(isOnline, THUMBNAIL_GREY_OUT_OPACITY);
        }

        public void SetMemberCount(string count) => textMembersCount.text = count;

        public void SetMemberCountVisible(bool visible) =>
            textMembersCount.gameObject.SetActive(visible);

        public void Activate(bool activate) => gameObject.SetActive(activate);

        public void SetAutoTranslateIndicatorForUserAndCommunities(bool isVisible) =>
            chatProfileView.SetAutoTranslateIndicator(isVisible);
    }
}
