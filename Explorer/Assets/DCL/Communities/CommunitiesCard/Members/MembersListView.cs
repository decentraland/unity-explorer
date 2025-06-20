using Cysharp.Threading.Tasks;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Profiles.Helpers;
using SuperScrollView;
using UnityEngine;
using DCL.UI.Utilities;
using DCL.Web3.Identities;
using MVC;
using Nethereum.Siwe.Core.Recap;
using System;
using System.Threading;
using UnityEngine.UI;
using Utility;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListView : MonoBehaviour, ICommunityFetchingView
    {
        public enum MemberListSections
        {
            ALL,
            BANNED
        }

        private const int ELEMENT_MISSING_THRESHOLD = 5;
        private const string KICK_MEMBER_TEXT_FORMAT = "Are you sure you want to kick '{0}' from {1}?";
        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban '{0}' from {1}?";
        private const string KICK_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string KICK_MEMBER_CONFIRM_TEXT = "KICK";
        private const string BAN_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string BAN_MEMBER_CONFIRM_TEXT = "BAN";

        [field: SerializeField] private ConfirmationDialogView confirmationDialogView { get; set; }
        [field: SerializeField] private LoopGridView loopGrid { get; set; }
        [field: SerializeField] private ScrollRect loopListScrollRect { get; set; }
        [field: SerializeField] private RectTransform sectionButtons { get; set; }
        [field: SerializeField] private RectTransform scrollViewRect { get; set; }
        [field: SerializeField] private MemberListSectionMapping[] memberListSectionsElements { get; set; }
        [field: SerializeField] private GameObject loadingObject { get; set; }

        [field: Header("Assets")]
        [field: SerializeField] private CommunityMemberListContextMenuConfiguration contextMenuSettings;
        [field: SerializeField] private Sprite kickSprite { get; set; }
        [field: SerializeField] private Sprite banSprite { get; set; }

        public event Action<MemberListSections> ActiveSectionChanged;
        public event Action? NewDataRequested;
        public event Action<MemberData>? ElementMainButtonClicked;
        public event Action<MemberData>? ElementFriendButtonClicked;
        public event Action<MemberData>? ElementUnbanButtonClicked;

        public event Action<UserProfileContextMenuControlSettings.UserData, UserProfileContextMenuControlSettings.FriendshipStatus>? ContextMenuUserProfileButtonClicked;
        public event Action<MemberData>? OpenProfilePassportRequested;
        public event Action<MemberData>? OpenUserChatRequested;
        public event Action<MemberData>? CallUserRequested;
        public event Action<MemberData>? BlockUserRequested;
        public event Action<MemberData>? RemoveModeratorRequested;
        public event Action<MemberData>? AddModeratorRequested;
        public event Action<MemberData>? KickUserRequested;
        public event Action<MemberData>? BanUserRequested;

        private float scrollViewMaxHeight;
        private float scrollViewHeight;
        private MemberListSections currentSection;
        private CancellationTokenSource confirmationDialogCts = new ();
        private Func<SectionFetchData<MemberData>> getCurrentSectionFetchData;
        private ProfileRepositoryWrapper profileRepositoryWrapper;
        private IWeb3IdentityCache web3IdentityCache;
        private IMVCManager mvcManager;
        private MemberData lastClickedProfileCtx;
        private GenericContextMenu contextMenu;
        private UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private GenericContextMenuElement removeModeratorContextMenuElement;
        private GenericContextMenuElement addModeratorContextMenuElement;
        private GenericContextMenuElement blockUserContextMenuElement;
        private GenericContextMenuElement kickUserContextMenuElement;
        private GenericContextMenuElement banUserContextMenuElement;
        private GenericContextMenuElement communityOptionsSeparatorContextMenuElement;
        private GetCommunityResponse.CommunityData? communityData = null;
        private CancellationToken cancellationToken;
        private UniTask panelTask;
        private bool viewerCanEdit => communityData?.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;

        private void Awake()
        {
            loopListScrollRect.SetScrollSensitivityBasedOnPlatform();
            scrollViewHeight = scrollViewRect.sizeDelta.y;
            scrollViewMaxHeight = scrollViewHeight + sectionButtons.sizeDelta.y;

            foreach (var sectionMapping in memberListSectionsElements)
                sectionMapping.Button.onClick.AddListener(() => ToggleSection(sectionMapping.Section));

            contextMenu = new GenericContextMenu(contextMenuSettings.ContextMenuWidth, verticalLayoutPadding: contextMenuSettings.VerticalPadding, elementsSpacing: contextMenuSettings.ElementsSpacing)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings((user, friendshipStatus) => ContextMenuUserProfileButtonClicked?.Invoke(user, friendshipStatus)))
                         .AddControl(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ViewProfileText, contextMenuSettings.ViewProfileSprite, () => OpenProfilePassportRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.ChatText, contextMenuSettings.ChatSprite, () => OpenUserChatRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(contextMenuSettings.CallText, contextMenuSettings.CallSprite, () => CallUserRequested?.Invoke(lastClickedProfileCtx!)))
                         .AddControl(blockUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BlockText, contextMenuSettings.BlockSprite, () => BlockUserRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(communityOptionsSeparatorContextMenuElement = new GenericContextMenuElement(new SeparatorContextMenuControlSettings(contextMenuSettings.SeparatorHeight, -contextMenuSettings.VerticalPadding.left, -contextMenuSettings.VerticalPadding.right)))
                         .AddControl(removeModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.RemoveModeratorText, contextMenuSettings.RemoveModeratorSprite, () => RemoveModeratorRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(addModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.AddModeratorText, contextMenuSettings.AddModeratorSprite, () => AddModeratorRequested?.Invoke(lastClickedProfileCtx!))))
                         .AddControl(kickUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.KickUserText, contextMenuSettings.KickUserSprite, () => ShowKickConfirmationDialog(lastClickedProfileCtx!, communityData?.name))))
                         .AddControl(banUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(contextMenuSettings.BanUserText, contextMenuSettings.BanUserSprite, () => ShowBanConfirmationDialog(lastClickedProfileCtx!, communityData?.name))));
        }

        private void OnDisable()
        {
            ToggleSection(MemberListSections.ALL);
            confirmationDialogCts.SafeCancelAndDispose();
        }

        private void OnContextMenuButtonClicked(MemberData profile, Vector2 buttonPosition, MemberListItemView elementView)
        {
            lastClickedProfileCtx = profile;
            userProfileContextMenuControlSettings.SetInitialData(profile.ToUserData(), profile.friendshipStatus.Convert());
            elementView.CanUnHover = false;

            removeModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.moderator && communityData?.role is CommunityMemberRole.owner;
            addModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.member && communityData?.role is CommunityMemberRole.owner;
            blockUserContextMenuElement.Enabled = profile.friendshipStatus != FriendshipStatus.blocked && profile.friendshipStatus != FriendshipStatus.blocked_by;
            kickUserContextMenuElement.Enabled = profile.role != CommunityMemberRole.owner && viewerCanEdit && currentSection == MemberListSections.ALL;
            banUserContextMenuElement.Enabled = profile.role != CommunityMemberRole.owner && viewerCanEdit && currentSection == MemberListSections.ALL;

            communityOptionsSeparatorContextMenuElement.Enabled = removeModeratorContextMenuElement.Enabled || addModeratorContextMenuElement.Enabled || kickUserContextMenuElement.Enabled || banUserContextMenuElement.Enabled;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition,
                           actionOnHide: () => elementView.CanUnHover = true,
                           closeTask: panelTask)), cancellationToken)
                      .Forget();
        }

        private void ShowKickConfirmationDialog(MemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowKickConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowKickConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(KICK_MEMBER_TEXT_FORMAT, profile.name, communityName),
                        KICK_MEMBER_CANCEL_TEXT,
                        KICK_MEMBER_CONFIRM_TEXT,
                        kickSprite,
                        false, false),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                KickUserRequested?.Invoke(profile);
            }
        }

        private void ShowBanConfirmationDialog(MemberData profile, string communityName)
        {
            confirmationDialogCts = confirmationDialogCts.SafeRestart();
            ShowBanConfirmationDialogAsync(confirmationDialogCts.Token).Forget();
            return;

            async UniTaskVoid ShowBanConfirmationDialogAsync(CancellationToken ct)
            {
                ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                    new ConfirmationDialogView.DialogData(string.Format(BAN_MEMBER_TEXT_FORMAT, profile.name, communityName),
                        BAN_MEMBER_CANCEL_TEXT,
                        BAN_MEMBER_CONFIRM_TEXT,
                        banSprite,
                        false, false),
                    ct);

                if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                BanUserRequested?.Invoke(profile);
            }
        }

        public void SetActive(bool active) => gameObject.SetActive(active);

        private void ToggleSection(MemberListSections section)
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
            ActiveSectionChanged?.Invoke(section);
        }

        public void SetSectionButtonsActive(bool isActive)
        {
            sectionButtons.gameObject.SetActive(isActive);
            scrollViewRect.sizeDelta = new Vector2(scrollViewRect.sizeDelta.x, isActive ? scrollViewHeight : scrollViewMaxHeight);
        }

        public void InitGrid(Func<SectionFetchData<MemberData>> currentSectionDataFunc,
            IWeb3IdentityCache web3IdentityCache,
            IMVCManager mvcManager)
        {
            loopGrid.InitGridView(0, GetLoopGridItemByIndex);
            getCurrentSectionFetchData = currentSectionDataFunc;
            this.web3IdentityCache = web3IdentityCache;
            this.mvcManager = mvcManager;
        }

        public void SetProfileDataProvider(ProfileRepositoryWrapper profileDataProvider)
        {
            this.profileRepositoryWrapper = profileDataProvider;
        }

        public void SetCommunityData(GetCommunityResponse.CommunityData community, UniTask panelTask, CancellationToken ct)
        {
            communityData = community;
            cancellationToken = ct;
            this.panelTask = panelTask;
        }

        private LoopGridViewItem GetLoopGridItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem listItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListItemView elementView = listItem.GetComponent<MemberListItemView>();

            SectionFetchData<MemberData> membersData = getCurrentSectionFetchData();

            MemberData memberData = membersData.items[index];
            elementView.Configure(memberData, currentSection, memberData.memberAddress.EqualsIgnoreCase(web3IdentityCache.Identity?.Address), profileRepositoryWrapper);

            elementView.SubscribeToInteractions(member => ElementMainButtonClicked?.Invoke(member),
                OnContextMenuButtonClicked,
                member => ElementFriendButtonClicked?.Invoke(member),
                member => ElementUnbanButtonClicked?.Invoke(member));

            if (index >= membersData.totalFetched - ELEMENT_MISSING_THRESHOLD && membersData.totalFetched < membersData.totalToFetch)
                NewDataRequested?.Invoke();

            return listItem;
        }

        public void RefreshGrid()
        {
            loopGrid.SetListItemCount(getCurrentSectionFetchData().items.Count, false);
            loopGrid.RefreshAllShownItem();
        }

        public void SetEmptyStateActive(bool active) { }

        public void SetLoadingStateActive(bool active) =>
            loadingObject.SetActive(active);

        [Serializable]
        public struct MemberListSectionMapping
        {
            [field: SerializeField]
            public MemberListSections Section { get; private set; }

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
        }
    }
}
