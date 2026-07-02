using Coffee.UISoftMask;
using DCL.CharacterPreview;
using DCL.InWorldCamera.CameraReelGallery;
using DCL.InWorldCamera.CameraReelGallery.Components;
using DCL.Passport.Modals;
using DCL.Passport.Modules;
using DCL.Passport.Modules.Badges;
using DCL.Passport.Modules.Creations;
using DCL.UI;
using DCL.UI.ProfileElements;
using MVC;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView
    {
        [field: SerializeField]
        public Button CloseButton { get; private set; } = null!;

        [field: SerializeField]
        public ScrollRect MainScroll { get; private set; } = null!;

        [field: SerializeField]
        public Button BackgroundButton { get; private set; } = null!;

        [field: SerializeField]
        public Image BackgroundImage { get; private set; } = null!;

        [field: SerializeField]
        public CharacterPreviewView CharacterPreviewView { get; private set; } = null!;

        [field: SerializeField]
        public UserBasicInfoPassportModuleView UserBasicInfoModuleView { get; private set; } = null!;

        [field: SerializeField]
        public UserDetailedInfoPassportModuleView UserDetailedInfoModuleView { get; private set; } = null!;

        [field: SerializeField]
        public EquippedItemsPassportModuleView EquippedItemsModuleView { get; private set; } = null!;

        [field: SerializeField]
        public BadgesOverviewPassportModuleView BadgesOverviewModuleView { get; private set; } = null!;

        [field: SerializeField]
        public BadgesDetailsPassportModuleView BadgesDetailsModuleView { get; private set; } = null!;

        [field: SerializeField]
        public CreationsDetailsPassportModuleView CreationsDetailsModuleView { get; private set; } = null!;

        [field: SerializeField]
        public BadgeInfoPassportModuleView BadgeInfoModuleView { get; private set; } = null!;

        [field: SerializeField]
        public CameraReelGalleryView CameraReelGalleryModuleView { get; private set; } = null!;

        [field: SerializeField]
        public CameraReelOptionButtonView CameraReelGalleryContextMenuView { get; private set; } = null!;

        [field: SerializeField]
        public AddLink_PassportModal AddLinkModal { get; private set; } = null!;

        [field: SerializeField]
        public WarningNotificationView ErrorNotification { get; private set; } = null!;

        [field: SerializeField]
        public List<SectionData> Sections = null!;

        [field: SerializeField]
        public SoftMask ViewportSoftMask { get; private set; } = null!;

        [field: SerializeField]
        public Image ViewportMaskGraphic { get; private set; } = null!;

        [field: SerializeField]
        public GameObject FriendInteractionContainer { get; private set; } = null!;

        [field: SerializeField]
        public Button AddFriendButton { get; private set; } = null!;

        [field: SerializeField]
        public Button AcceptFriendButton { get; private set; } = null!;

        [field: SerializeField]
        public Button RemoveFriendButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CancelFriendButton { get; private set; } = null!;

        [field: SerializeField]
        public Button UnblockFriendButton { get; private set; } = null!;

        [field: SerializeField]
        public MutualFriendsConfig MutualFriends { get; private set; }

        [field: SerializeField]
        public Button JumpInButton { get; private set; } = null!;

        [field: SerializeField]
        public Button ChatButton { get; private set; } = null!;

        [field: SerializeField]
        public Button CallButton { get; private set; } = null!;

        [field: Header("Context menu")]
        [field: SerializeField]
        public Button ContextMenuButton { get; private set; } = null!;

        [field: SerializeField]
        public Sprite BlockSprite { get; private set; } = null!;

        [field: SerializeField]
        public string BlockText { get; private set; } = "Block";

        [field: SerializeField]
        public Sprite ReportOptionSprite { get; private set; } = null!;

        [field: SerializeField]
        public Sprite ReportSprite { get; private set; } = null!;

        [field: SerializeField]
        public string ReportText { get; private set; } = "Report";

        [field: SerializeField]
        public Sprite JumpInSprite { get; private set; } = null!;

        [field: SerializeField]
        public string JumpInText { get; private set; } = "Jump to Location";

        [field: SerializeField]
        public Sprite GiftSprite { get; private set; } = null!;

        [field: SerializeField]
        public string GiftText { get; private set; } = "Gift";

        [field: SerializeField]
        public Sprite InviteToCommunitySprite { get; private set; } = null!;

        [field: SerializeField]
        public string InviteToCommunityText { get; private set; } = "Invite to Community";

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

        [Serializable]
        public class SectionData
        {
            public PassportSection PassportSection;
            public ButtonWithSelectableStateView ButtonWithState = null!;
            public GameObject Panel = null!;
        }

#if UNITY_EDITOR
        private void Awake()
        {
            // Copy material in the editor so we don't get asset changes
            BackgroundImage.material = new Material(BackgroundImage.material);
        }
#endif

        public void OpenSection(PassportSection passportSection)
        {
            foreach (var section in Sections)
            {
                bool isActive = passportSection == section.PassportSection;
                section.ButtonWithState.SetSelected(isActive);
                section.Panel.SetActive(isActive);

                if (isActive)
                    MainScroll.content = section.Panel.transform as RectTransform;
            }

            BadgeInfoModuleView.gameObject.SetActive(passportSection == PassportSection.BADGES);

            bool isNotPhotos = passportSection != PassportSection.PHOTOS;
            ViewportSoftMask.enabled = isNotPhotos;
            ViewportMaskGraphic.enabled = isNotPhotos;

            MainScroll.verticalNormalizedPosition = 1;
        }
    }
}
