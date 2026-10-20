using DCL.Friends.UI.FriendPanel;
using DCL.Notifications.NotificationsMenu;
using DCL.UI.Buttons;
using DCL.UI.Credits;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Sidebar.HelpMenu;
using DCL.UI.Skybox;
using DCL.VoiceChat.UI;
using MVC;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DCL.UI.Sidebar
{
    public class SidebarView : ViewBase, IView
    {
        public delegate void AutohideStatusChangedDelegate(bool status);
        public delegate void BlockStatusChangedDelegate(bool status);
        public event AutohideStatusChangedDelegate? AutohideStatusChanged;

        public event BlockStatusChangedDelegate? BlockStatusChanged;

        [field: Header("Map")]
        [field: SerializeField] public SimpleHoverableButton MapButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject minimapMenu { get; private set; } = null!;

        [field: Header("Discover")]
        [field: SerializeField] public SimpleHoverableButton DiscoverButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject discoverMenu { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView discoverPlacesOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView discoverEventsOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView discoverCommunitiesOption { get; private set; } = null!;
        [field: SerializeField] internal ButtonView discoverCloseButton { get; private set; } = null!;

        [field: Header("Shop")]
        [field: SerializeField] public SimpleHoverableButton ShopButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject shopMenu { get; private set; } = null!;
        [field: SerializeField] internal ButtonView shopEmotesBanner { get; private set; } = null!;
        [field: SerializeField] internal ButtonView shopWearablesBanner { get; private set; } = null!;
        [field: SerializeField] public CreditsPanelView ShopCreditsPanel { get; private set; } = null!;
        [field: SerializeField] internal ButtonView shopCloseButton { get; private set; } = null!;

        [field: Header("System Settings")]
        [field: SerializeField] public SimpleHoverableButton SettingsButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject systemMenu { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemSettingsOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemControlsOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemContactSupportOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemReportBugOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemDiscordOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView systemFaqOption { get; private set; } = null!;
        [field: SerializeField] internal ButtonView systemCloseButton { get; private set; } = null!;

        [field: Header("Audio Settings")]
        [field: SerializeField] public SimpleHoverableButton AudioSettingsButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject audioMenu { get; private set; } = null!;
        [field: SerializeField] internal Slider audioMasterSlider { get; private set; } = null!;
        [field: SerializeField] internal Slider audioInWorldSlider { get; private set; } = null!;
        [field: SerializeField] internal Slider audioUiSlider { get; private set; } = null!;
        [field: SerializeField] internal Slider audioAvatarSlider { get; private set; } = null!;
        [field: SerializeField] internal ButtonView audioCloseButton { get; private set; } = null!;

        [field: Header("Gallery")]
        [field: SerializeField] public SimpleHoverableButton GalleryButton { get; private set; } = null!;
        [field: SerializeField] internal GameObject galleryMenu { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView galleryGalleryOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView galleryTakeShotOption { get; private set; } = null!;
        [field: SerializeField] internal ButtonView galleryCloseButton { get; private set; } = null!;

        [field: Header("Friends")]
        [field: SerializeField] public SimpleHoverableButton FriendsButton { get; private set; } = null!;
        [field: SerializeField] public PersistentFriendPanelOpenerView PersistentFriendsPanelOpener { get; private set; } = null!;
        [field: SerializeField] public NotificationIndicatorView FriendRequestNotificationIndicator { get; private set; } = null!;

        [field: Header("Emotes")]
        [field: SerializeField] public SimpleHoverableButton EmotesButton { get; private set; } = null!;

        [field: Header("Notifications")]
        [field: SerializeField] public SimpleHoverableButton NotificationsButton { get; private set; } = null!;
        [field: SerializeField] public NotificationsMenuView NotificationsMenuView { get; private set; } = null!;

        [field: Header("Profile")]
        [field: SerializeField] public ProfileWidgetView ProfileWidget { get; private set; } = null!;
        [field: SerializeField] internal GameObject profileMenu { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProfileNameLabel { get; private set; } = null!;
        [field: SerializeField] public GameObject ProfileVerifiedMark { get; private set; } = null!;
        [field: SerializeField] public GameObject ProfileFoundationMark { get; private set; } = null!;
        [field: SerializeField] public TMP_Text ProfileWalletAddressLabel { get; private set; } = null!;
        [field: SerializeField] public Button ProfileCopyWalletButton { get; private set; } = null!;
        [field: SerializeField] public Button ProfileInviteFriendsButton { get; private set; } = null!;
        [field: SerializeField] public ProfilePictureView ProfilePicture { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView ProfileCopyWalletToast { get; private set; } = null!;
        [field: SerializeField] public WarningNotificationView ProfileInviteToast { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView profileBadgesOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView profileProfileOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView profileBackpackOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView profileSignOutOption { get; private set; } = null!;
        [field: SerializeField] internal MenuElementView profileExitOption { get; private set; } = null!;
        [field: SerializeField] internal ButtonView profileCloseButton { get; private set; } = null!;

        [field: Header("Chat")]
        [field: SerializeField]
        [field: FormerlySerializedAs("<unreadMessagesButton>k__BackingField")]
        public HoverableAndSelectableButtonWithAnimator UnreadMessagesButton { get; private set; } = null!;

        [field: Header("Nearby Voice Chat")]
        [field: SerializeField] public NearbyVoiceChatButtonView NearbyVoiceChatButton { get; private set; } = null!;
        [field: SerializeField] public NearbyVoiceWidgetView NearbyVoiceWidget { get; private set; } = null!;
        [field: SerializeField] public NearbyVoiceTipView NearbyVoiceTip { get; private set; } = null!;

        [field: Header("Marketplace Credits")]
        [field: SerializeField]
        [field: FormerlySerializedAs("<marketplaceCreditsButton>k__BackingField")]
        public HoverableAndSelectableButtonWithAnimator MarketplaceCreditsButton { get; private set; } = null!;

        [field: SerializeField]
        [field: FormerlySerializedAs("<marketplaceCreditsButtonAnimator>k__BackingField")]
        public Animator MarketplaceCreditsButtonAnimator { get; private set; } = null!;

        [field: SerializeField]
        [field: FormerlySerializedAs("<marketplaceCreditsButtonAlertMark>k__BackingField")]
        public GameObject MarketplaceCreditsButtonAlertMark { get; private set; } = null!;

        [field: Header("Pending Port")]
        [field: SerializeField] public ProfileMenuView ProfileMenuView { get; private set; } = null!;
        [field: SerializeField] public SkyboxMenuView SkyboxMenuView { get; private set; } = null!;
        [field: SerializeField] public SmartWearablesSideBarTooltipView SmartWearablesTooltipView { get; private set; } = null!;
        [field: SerializeField] public SidebarConfigPanelView SidebarConfigPanelView { get; private set; } = null!;
        [field: SerializeField] public HelpMenuView HelpMenu { get; private set; } = null!;

        public void BlockSidebar()
        {
            BlockStatusChanged?.Invoke(true);
        }

        public void SetAutoHideSidebarStatus(bool value)
        {
            AutohideStatusChanged?.Invoke(value);
        }

        public void UnblockSidebar()
        {
            BlockStatusChanged?.Invoke(false);
        }
    }
}
