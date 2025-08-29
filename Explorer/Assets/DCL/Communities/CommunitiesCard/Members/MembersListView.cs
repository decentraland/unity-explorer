using Cysharp.Threading.Tasks;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Friends.UI.FriendPanel;
using DCL.UI;
using DCL.UI.ConfirmationDialog.Opener;
using DCL.UI.GenericContextMenu.Controllers.Communities;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;
using DCL.Utilities.Extensions;
using MVC;
using Nethereum.Siwe.Core.Recap;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;
using Utility.Types;
using MemberData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour, ICommunityFetchingView<ICommunityMemberData>
    {
        public enum MemberListSections
        {
            MEMBERS,
            BANNED,
            REQUESTS,
            INVITES
        }

        private const int ELEMENT_MISSING_THRESHOLD = 5;
        private const string KICK_MEMBER_TEXT_FORMAT = "Are you sure you want to remove [{0}] from the [{1}] Community?";
        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban [{0}] from the [{1}] Community?";
        private const string KICK_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string KICK_MEMBER_CONFIRM_TEXT = "KICK";
        private const string BAN_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string BAN_MEMBER_CONFIRM_TEXT = "BAN";

        [field: SerializeField] private LoopGridView loopGrid { get; set; } = null!;
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; } = null!;
        [field: SerializeField] private RectTransform sectionButtons { get; set; } = null!;
        [field: SerializeField] private RectTransform scrollViewRect { get; set; } = null!;
        [field: SerializeField] private MemberListSectionMapping[] memberListSectionsElements { get; set; } = null!;
        [field: SerializeField] private SkeletonLoadingView loadingObject { get; set; } = null!;
        [field: SerializeField] private NotificationIndicatorView requestsNotificationIndicator { get; set; } = null!;
        [field: SerializeField] private GameObject emptyStateParent { get; set; } = null!;

        [field: Header("Assets")]
        [field: SerializeField] private CommunityMemberListContextMenuConfiguration contextMenuSettings = null!;
        [field: SerializeField] private Sprite kickSprite { get; set; } = null!;
        [field: SerializeField] private Sprite banSprite { get; set; } = null!;

        public event Action<MemberListSections>? ActiveSectionChanged;
        public event Action? NewDataRequested;
        public event Action<ICommunityMemberData>? ElementMainButtonClicked;
        public event Action<ICommunityMemberData>? ElementFriendButtonClicked;
        public event Action<ICommunityMemberData>? ElementUnbanButtonClicked;
        public event Action<ICommunityMemberData, InviteRequestIntention>? ElementManageRequestClicked;

        public event Action<UserProfileContextMenuControlSettings.UserData, UserProfileContextMenuControlSettings.FriendshipStatus>? ContextMenuUserProfileButtonClicked;
        public event Action<ICommunityMemberData>? OpenProfilePassportRequested;
        public event Action<ICommunityMemberData>? OpenUserChatRequested;
        public event Action<ICommunityMemberData>? CallUserRequested;
        public event Action<ICommunityMemberData>? BlockUserRequested;
        public event Action<ICommunityMemberData>? RemoveModeratorRequested;
        public event Action<ICommunityMemberData>? AddModeratorRequested;
        public event Action<ICommunityMemberData>? KickUserRequested;
        public event Action<ICommunityMemberData>? BanUserRequested;

        private float scrollViewMaxHeight;
        private float scrollViewHeight;
        private MemberListSections currentSection;
        private CancellationTokenSource confirmationDialogCts = new ();
        private SectionFetchData<ICommunityMemberData> membersData = null!;
        private ProfileRepositoryWrapper? profileRepositoryWrapper;
        private ICommunityMemberData lastClickedProfileCtx = null!;
        private GenericContextMenu? contextMenu;
        private UserProfileContextMenuControlSettings? userProfileContextMenuControlSettings;
        private GenericContextMenuElement? removeModeratorContextMenuElement;
        private GenericContextMenuElement? addModeratorContextMenuElement;
        private GenericContextMenuElement? blockUserContextMenuElement;
        private GenericContextMenuElement? kickUserContextMenuElement;
        private GenericContextMenuElement? banUserContextMenuElement;
        private GenericContextMenuElement? communityOptionsSeparatorContextMenuElement;
        private GetCommunityResponse.CommunityData? communityData;
        private CancellationTokenSource contextMenuCts = new ();
        private UniTask panelTask;
        private bool viewerCanEdit => communityData?.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;

        private CommunityInvitationContextMenuButtonHandler? invitationButtonHandler;
        private CommunitiesDataProvider.CommunitiesDataProvider? communitiesDataProvider;
        private INotificationsBusController? notificationsBus;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = scrollViewRect.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + sectionButtons.sizeDelta.y;

            foreach (var sectionMapping in memberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding, elementsSpacing: contextMenuSettings.ElementsSpacing, showRim: true)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((user, friendshipStatus) => ContextMenuUserProfileButtonClicked?.Invoke(user, friendshipStatus), showProfilePicture: false))
                         .AddControl(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewProfileText, contextMenuSettings.ViewProfileSprite, () => OpenProfilePassportRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ChatText, contextMenuSettings.ChatSprite, () => OpenUserChatRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CallText, contextMenuSettings.CallSprite, () => CallUserRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(blockUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BlockText, contextMenuSettings.BlockSprite, () => BlockUserRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(communityOptionsSeparatorContextMenuElement = new GenericContextMenuElement(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right)))
                         .AddControl(removeModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.RemoveModeratorText, contextMenuSettings.RemoveModeratorSprite, () => RemoveModeratorRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(addModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.AddModeratorText, contextMenuSettings.AddModeratorSprite, () => AddModeratorRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(kickUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.KickUserText, contextMenuSettings.KickUserSprite, () => ShowKickConfirmationDialog(lastClickedProfileCtx!, communityData?.name!))))
                         .AddControl(banUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BanUserText, contextMenuSettings.BanUserSprite, () => ShowBanConfirmationDialog(lastClickedProfileCtx!, communityData?.name!))));
        }

        public void Close()
        {
            ToggleSection(MemberListSections.MEMBERS, false);
            confirmationDialogCts.SafeCancelAndDispose();
        }

        public void UpdateRequestsCounter(int amount) =>
            requestsNotificationIndicator.SetNotificationCount(amount);

        private void OnContextMenuButtonClicked(ICommunityMemberData profile, Vector2 buttonPosition, MemberListItemView elementView)
        {
            lastClickedProfileCtx = profile;
            contextMenuCts = contextMenuCts.SafeRestart();
            UserProfileContextMenuControlSettings.FriendshipStatus status = profile.FriendshipStatus.Convert();
            // Disable all buttons and leave only the unfriend one, as part of the UI/UX decision. The old passed value was:
            // status == UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED ? UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED : status
            userProfileContextMenuControlSettings!.SetInitialData(profile.ToUserData(), status == UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND ? status : UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED);
            elementView.CanUnHover = false;

            removeModeratorContextMenuElement!.Enabled = profile.Role == CommunityMemberRole.moderator && communityData?.role is CommunityMemberRole.owner;
            addModeratorContextMenuElement!.Enabled = profile.Role == CommunityMemberRole.member && communityData?.role is CommunityMemberRole.owner;
            blockUserContextMenuElement!.Enabled = profile.FriendshipStatus != FriendshipStatus.blocked && profile.FriendshipStatus != FriendshipStatus.blocked_by;
            kickUserContextMenuElement!.Enabled = profile.Role != CommunityMemberRole.owner && viewerCanEdit && currentSection == MemberListSections.MEMBERS;
            banUserContextMenuElement!.Enabled = profile.Role != CommunityMemberRole.owner && viewerCanEdit && currentSection == MemberListSections.MEMBERS;

            communityOptionsSeparatorContextMenuElement!.Enabled = removeModeratorContextMenuElement.Enabled || addModeratorContextMenuElement.Enabled || kickUserContextMenuElement.Enabled || banUserContextMenuElement.Enabled;

            if (invitationButtonHandler == null)
            {
                invitationButtonHandler = new CommunityInvitationContextMenuButtonHandler(communitiesDataProvider, notificationsBus, contextMenuSettings.ElementsSpacing);
                invitationButtonHandler.AddSubmenuControlToContextMenu(contextMenu, contextMenuSettings.InviteToCommunityText, contextMenuSettings.InviteToCommunitySprite);
            }

            invitationButtonHandler.SetUserToInvite(profile.Address);

            ViewDependencies.ContextMenuOpener.OpenContextMenu(new GenericContextMenuParameter(contextMenu, buttonPosition,
                           actionOnHide: () => elementView.CanUnHover = true,
                           closeTask: panelTask), contextMenuCts.Token);
        }

        private void ShowKickConfirmationDialog(ICommunityMemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowKickConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowKickConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(KICK_MEMBER_TEXT_FORMAT, profile.Name, communityName),
                                                                                         KICK_MEMBER_CANCEL_TEXT,
                                                                                         KICK_MEMBER_CONFIRM_TEXT,
                                                                                         kickSprite,
                                                                                         false, false,
                                                                                         userInfo: new ConfirmationDialogParameter.UserData(profile.Address, profile.ProfilePictureUrl, profile.GetUserNameColor())),
                                                                                     ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                KickUserRequested?.Invoke(profile);
            }
        }

        private void ShowBanConfirmationDialog(ICommunityMemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowBanConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowBanConfirmationDialogAsync(CancellationToken ct)
            {
                Result<ConfirmationResult> dialogResult = await ViewDependencies.ConfirmationDialogOpener.OpenConfirmationDialogAsync(new ConfirmationDialogParameter(string.Format(BAN_MEMBER_TEXT_FORMAT, profile.Name, communityName),
                                                                                         BAN_MEMBER_CANCEL_TEXT,
                                                                                         BAN_MEMBER_CONFIRM_TEXT,
                                                                                         banSprite,
                                                                                         false, false,
                                                                                         userInfo: new ConfirmationDialogParameter.UserData(profile.Address, profile.ProfilePictureUrl, profile.GetUserNameColor())),
                                                                                     ct)
                                                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested || !dialogResult.Success || dialogResult.Value == ConfirmationResult.CANCEL) return;

                BanUserRequested?.Invoke(profile);
            }
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        private void ToggleSection(MemberListSections section, bool invokeEvent = true)
        {
            if (currentSection == section) return;

            foreach (var sectionMapping in memberListSectionsElements)
            {
                sectionMapping.SelectedBackground.SetActive(sectionMapping.Section == section);
                sectionMapping.SelectedText.SetActive(sectionMapping.Section == section);
                sectionMapping.UnselectedBackground.SetActive(sectionMapping.Section != section);
                sectionMapping.UnselectedText.SetActive(sectionMapping.Section != section);
            }

            currentSection = section;
            if (invokeEvent)
                ActiveSectionChanged?.Invoke(section);
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            sectionButtons.gameObject.SetActive(isActive);
            scrollViewRect.sizeDelta = new Vector2(scrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
        }

        public void InitGrid()
        {
            loopGrid.InitGridView(0, GetLoopGridItemByIndex);
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            this.profileRepositoryWrapper = profileDataProvider;
        }

        public void SetCommunitiesDataProvider(CommunitiesDataProvider.CommunitiesDataProvider dataProvider)
        {
            this.communitiesDataProvider = dataProvider;
        }

        public void SetNotificationsBusController(INotificationsBusController notificationsBusController)
        {
            this.notificationsBus = notificationsBusController;
        }

        public void SetCommunityData(GetCommunityResponse.CommunityData community, UniTask panelTask)
        {
            communityData = community;
            this.panelTask = panelTask;

            foreach (var sectionMapping in memberListSectionsElements)
            {
                sectionMapping.Button.gameObject.SetActive(true);

                if (community.privacy != CommunityPrivacy.@private && sectionMapping.ForPrivateCommunitiesOnly)
                    sectionMapping.Button.gameObject.SetActive(false);
            }

        }

        private LoopGridViewItem GetLoopGridItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem listItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListItemView elementView = listItem.GetComponent<MemberListItemView>();

            ICommunityMemberData memberData = membersData.Items[index];
            elementView.Configure(memberData, currentSection, memberData.Address.EqualsIgnoreCase(ViewDependencies.CurrentIdentity?.Address), profileRepositoryWrapper);

            elementView.SubscribeToInteractions(member => ElementMainButtonClicked?.Invoke(member),
                OnContextMenuButtonClicked,
                member => ElementFriendButtonClicked?.Invoke(member),
                member => ElementUnbanButtonClicked?.Invoke(member),
                (member, intention) => ElementManageRequestClicked?.Invoke(member, intention));

            if (index >= membersData.TotalFetched - ELEMENT_MISSING_THRESHOLD && membersData.TotalFetched < membersData.TotalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        public void RefreshGrid(SectionFetchData<ICommunityMemberData> data, bool redraw)
        {
            membersData = data;

            loopGrid.SetListItemCount(membersData.Items.Count, false);

            if (redraw)
                loopGrid.RefreshAllShownItem();
        }

        public void SetEmptyStateActive(bool active)
        {
            if (active)
            {
                emptyStateParent.SetActive(true);
                foreach (var sectionMapping in memberListSectionsElements)
                    sectionMapping.EmptyState.SetActive(sectionMapping.Section == currentSection);
            }
            else
                emptyStateParent.SetActive(false);
        }

        public void SetLoadingStateActive(bool active)
        {
            if (active)
                loadingObject.ShowLoading();
            else
                loadingObject.HideLoading();
        }

        [Serializable]
        public struct MemberListSectionMapping
        {
            [field: SerializeField]
            public MemberListSections Section { get; private set; }

            [field: SerializeField]
            public bool ForPrivateCommunitiesOnly { get; private set; }

            [field: SerializeField]
            public Button Button { get; private set; }

            [field: SerializeField]
            public GameObject SelectedBackground { get; private set; }

            [field: SerializeField]
            public GameObject SelectedText { get; private set; }

            [field: SerializeField]
            public GameObject UnselectedBackground { get; private set; }

            [field: SerializeField]
            public GameObject UnselectedText { get; private set; }

            [field: Space(10)]
            [field: SerializeField]
            public GameObject EmptyState { get; private set; }
        }
    }
}
