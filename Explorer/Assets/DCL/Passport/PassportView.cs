using DCL.CharacterPreview;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.Passport.Modals;
using DCL.Passport.Modules;
using DCL.Passport.Modules.Badges;
using DCL.UI;
using DCL.UI.ProfileElements;
using MVC;
using SoftMasking;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView, IViewWithGlobalDependencies
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; }

        [field: SerializeField]
        public ScrollRect MainScroll { get; private set; }

        [field: SerializeField]
        public Button BackgroundButton { get; private set; }

        [field: SerializeField]
        public Image BackgroundImage { get; private set; }

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; }

        [field: SerializeField]
        public UserBasicInfo_PassportModuleView UserBasicInfoModuleView { get; private set; }

        [field: SerializeField]
        public UserDetailedInfo_PassportModuleView UserDetailedInfoModuleView { get; private set; }

        [field: SerializeField]
        public EquippedItems_PassportModuleView EquippedItemsModuleView { get; private set; }

        [field: SerializeField]
        public BadgesOverview_PassportModuleView BadgesOverviewModuleView { get; private set; }

        [field: SerializeField]
        public BadgesDetails_PassportModuleView BadgesDetailsModuleView { get; private set; }

        [field: SerializeField]
        public BadgeInfo_PassportModuleView BadgeInfoModuleView { get; private set; }

        [field: SerializeField]
        public CameraReelGalleryView CameraReelGalleryModuleView { get; private set; }

        [field: SerializeField]
        public RectTransform MainContainer { get; private set; }

        [field: SerializeField]
        public AddLink_PassportModal AddLinkModal { get; private set; }

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; }

        [field: SerializeField]
        public ButtonWithSelectableStateView OverviewSectionButton { get; private set; }

        [field: SerializeField]
        public ButtonWithSelectableStateView BadgesSectionButton { get; private set; }

        [field: SerializeField]
        public ButtonWithSelectableStateView PhotosSectionButton { get; private set; }

        [field: SerializeField]
        public GameObject OverviewSectionPanel { get; private set; }

        [field: SerializeField]
        public GameObject BadgesSectionPanel { get; private set; }

        [field: SerializeField]
        public GameObject PhotosSectionPanel { get; private set; }

        [field: SerializeField]
        public SoftMask ViewportSoftMask { get; private set; }

        [field: SerializeField]
        public GameObject FriendInteractionContainer { get; private set; }

        [field: SerializeField]
        public Button AddFriendButton { get; private set; }

        [field: SerializeField]
        public Button AcceptFriendButton { get; private set; }

        [field: SerializeField]
        public Button RemoveFriendButton { get; private set; }

        [field: SerializeField]
        public Button CancelFriendButton { get; private set; }

        [field: SerializeField]
        public Button UnblockFriendButton { get; private set; }

        [field: SerializeField]
        public MutualFriendsConfig MutualFriends { get; private set; }

        [field: SerializeField]
        public Button JumpInButton { get; private set; }

        [field: SerializeField]
        public Button VoiceChatButton { get; private set; }

        [field: SerializeField]
        public Button ChatButton { get; private set; }

        [field: Header("Context menu")]
        [field: SerializeField]
        public Button ContextMenuButton { get; private set; }

        [field: SerializeField]
        public Sprite BlockSprite { get; private set; }

        [field: SerializeField]
        public string BlockText { get; private set; } = "Block";

        [field: SerializeField]
        public Sprite JumpInSprite { get; private set; }

        [field: SerializeField]
        public string JumpInText { get; private set; } = "Jump to Location";

        [Serializable]
        public struct MutualFriendsConfig
        {
            public GameObject Root;
            public MutualThumbnail[] Thumbnails;
            public TMP_Text AmountLabel;

            [Serializable]
            public struct MutualThumbnail
            {
                public GameObject Root;
                public ProfilePictureView Picture;
            }
        }

#if UNITY_EDITOR
        private void Awake()
        {
            // Copy material in editor so we don't get asset changes
            BackgroundImage.material = new Material(BackgroundImage.material);
        }
#endif

        public void OpenPhotosSection()
        {
            OverviewSectionButton.SetSelected(false);
            BadgesSectionButton.SetSelected(false);
            PhotosSectionButton.SetSelected(true);

            OverviewSectionPanel.SetActive(false);
            PhotosSectionPanel.SetActive(true);
            BadgesSectionPanel.SetActive(false);
            BadgeInfoModuleView.gameObject.SetActive(false);
            ViewportSoftMask.enabled = false;
            MainScroll.content = PhotosSectionPanel.transform as RectTransform;
            MainScroll.verticalNormalizedPosition = 1;
        }

        public void OpenBadgesSection()
        {
            OverviewSectionButton.SetSelected(false);
            BadgesSectionButton.SetSelected(true);
            PhotosSectionButton.SetSelected(false);
            OverviewSectionPanel.SetActive(false);
            BadgesSectionPanel.SetActive(true);
            PhotosSectionPanel.SetActive(false);
            ViewportSoftMask.enabled = true;
            MainScroll.content = BadgesSectionPanel.transform as RectTransform;
            MainScroll.verticalNormalizedPosition = 1;
            CharacterPreviewView.gameObject.SetActive(false);
        }

        public void OpenOverviewSection()
        {
            OverviewSectionButton.SetSelected(true);
            BadgesSectionButton.SetSelected(false);
            PhotosSectionButton.SetSelected(false);
            OverviewSectionPanel.SetActive(true);
            BadgesSectionPanel.SetActive(false);
            PhotosSectionPanel.SetActive(false);
            ViewportSoftMask.enabled = true;
            MainScroll.content = OverviewSectionPanel.transform as RectTransform;
            MainScroll.verticalNormalizedPosition = 1;
            CharacterPreviewView.gameObject.SetActive(true);
        }

        public void InjectDependencies(ViewDependencies dependencies)
        {
            foreach (MutualFriendsConfig.MutualThumbnail thumbnail in MutualFriends.Thumbnails)
                thumbnail.Picture.InjectDependencies(dependencies);
        }
    }
}
