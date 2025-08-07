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
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.Passport
{
    public class PassportView : ViewBase, IView
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

        // TODO insert here new list

        [field: SerializeField]
        public List<SectionData> Sections;

        // [field: SerializeField]
        // public ButtonWithSelectableStateView OverviewSectionButton { get; private set; }
        //
        // [field: SerializeField]
        // public ButtonWithSelectableStateView BadgesSectionButton { get; private set; }
        //
        // [field: SerializeField]
        // public ButtonWithSelectableStateView PhotosSectionButton { get; private set; }
        //
        // [field: SerializeField]
        // public GameObject OverviewSectionPanel { get; private set; }
        //
        // [field: SerializeField]
        // public GameObject BadgesSectionPanel { get; private set; }
        //
        // [field: SerializeField]
        // public GameObject PhotosSectionPanel { get; private set; }

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
        public Button ChatButton { get; private set; }

        [field: SerializeField]
        public Button CallButton { get; private set; }

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

        [Serializable]
        public class SectionData
        {
            public PassportSection PassportSection;
            public ButtonWithSelectableStateView ButtonWithState;
            public GameObject Panel;
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

            // OverviewSectionButton.SetSelected(passportSection == PassportSection.OVERVIEW);
            // BadgesSectionButton.SetSelected(passportSection == PassportSection.BADGES);
            // PhotosSectionButton.SetSelected(passportSection == PassportSection.PHOTOS);

            // OverviewSectionPanel.SetActive(passportSection == PassportSection.OVERVIEW);
            // BadgesSectionPanel.SetActive(passportSection == PassportSection.BADGES);
            // PhotosSectionPanel.SetActive(passportSection == PassportSection.PHOTOS);

            BadgeInfoModuleView.gameObject.SetActive(passportSection == PassportSection.BADGES);

            ViewportSoftMask.enabled = passportSection != PassportSection.PHOTOS;

            // switch (passportSection)
            // {
            //     case PassportSection.OVERVIEW:
            //         MainScroll.content = OverviewSectionPanel.transform as RectTransform;
            //         break;
            //     case PassportSection.BADGES:
            //         MainScroll.content = BadgesSectionPanel.transform as RectTransform;
            //         break;
            //     case PassportSection.PHOTOS:
            //         MainScroll.content = PhotosSectionPanel.transform as RectTransform;
            //         break;
            // }
            MainScroll.verticalNormalizedPosition = 1;

            CharacterPreviewView.gameObject.SetActive(passportSection != PassportSection.BADGES);
        }
    }
}
