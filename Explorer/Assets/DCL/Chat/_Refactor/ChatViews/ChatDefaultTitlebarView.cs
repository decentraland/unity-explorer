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
        public event Action? OnCloseRequested;
        public event Action? OnMembersRequested;
        public event Action? OnContextMenuRequested;
        public event Action<TitlebarViewMode>? OnProfileContextMenuRequested;

        public Button ButtonClose => buttonClose;
        public Button ButtonOpenMembers => buttonOpenMembers;
        public Button ButtonOpenContextMenu => buttonOpenContextMenu;
        public Button ButtonOpenProfileContextMenu => buttonOpenProfileContextMenu;
        public CallButtonView ButtonStartCall => buttonStartCall;


        [SerializeField] private Button buttonClose;
        [SerializeField] private Button buttonOpenMembers;
        [SerializeField] private Button buttonOpenContextMenu;
        [SerializeField] private Button buttonOpenProfileContextMenu;

        [SerializeField] private CallButtonView buttonStartCall;

        [SerializeField] private TMP_Text textChannelName;
        [SerializeField] private TMP_Text textMembersCount;
        [SerializeField] private ChatProfileView chatProfileView;
        [SerializeField] private GameObject nearbyElementsContainer;
        [SerializeField] private GameObject nearbyAutoTranslateIndicator;

        private TitlebarViewMode currentViewMode;
        private ChatTitlebarViewModel currentTitlebarViewModel;
        private ColorBlock profileCtxMenuButtonNormalColors;
        private ColorBlock profileCtxMenuButtonOpenColors;

        [SerializeField]
        private Image connectionStatusIndicator;

        [Range(0.0f, 1.0f)]
        [SerializeField] private float offlineThumbnailGreyOutOpacity = 0.6f;

        private void Awake()
        {
            buttonOpenContextMenu.onClick.AddListener(() => OnContextMenuRequested?.Invoke());
            buttonOpenProfileContextMenu.onClick.AddListener(() => OnProfileContextMenuRequested?.Invoke(currentViewMode));
            buttonClose.onClick.AddListener(() => OnCloseRequested?.Invoke());
            buttonOpenMembers.onClick.AddListener(() => OnMembersRequested?.Invoke());

            profileCtxMenuButtonNormalColors = buttonOpenProfileContextMenu.colors;
            profileCtxMenuButtonOpenColors = buttonOpenProfileContextMenu.colors;
            profileCtxMenuButtonOpenColors.normalColor = profileCtxMenuButtonOpenColors.pressedColor;
        }

        public void SetContextMenuButtonSelectedAppearance() =>
            buttonOpenProfileContextMenu.colors = profileCtxMenuButtonOpenColors;

        public void SetContextMenuButtonNormalAppearance() =>
            buttonOpenProfileContextMenu.colors = profileCtxMenuButtonNormalColors;

        public void Setup(ChatTitlebarViewModel model)
        {
            currentTitlebarViewModel = model;
            currentViewMode = model.ViewMode;
            textChannelName.text = model.Username;

            bool shouldShowMembersButton = model.ViewMode == TitlebarViewMode.Nearby ||
                                           model.ViewMode == TitlebarViewMode.Community;

            buttonOpenMembers.gameObject.SetActive(shouldShowMembersButton);

            if (model.Thumbnail.Value.ThumbnailState
                is ProfileThumbnailViewModel.State.LOADING or
                ProfileThumbnailViewModel.State.NOT_BOUND)
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

        public void SetConnectionStatus(bool isOnline)
        {
            connectionStatusIndicator.gameObject.SetActive(isOnline);
            if (chatProfileView != null)
                chatProfileView.SetConnectionStatus(isOnline, offlineThumbnailGreyOutOpacity);
        }

        public void SetMemberCount(string count) => textMembersCount.text = count;
        public void Activate(bool activate) => gameObject.SetActive(activate);

        public void SetAutoTranslateIndicatorForUserAndCommunities(bool isVisible)
        {
            chatProfileView.SetAutoTranslateIndicator(isVisible);
        }
    }
}
