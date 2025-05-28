using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.Requests;
using DCL.Passport;
using DCL.UI.GenericContextMenu;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.Utilities;
using DCL.Web3;
using MVC;
using SuperScrollView;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : IDisposable
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int PAGE_SIZE = 20;
        private const int ELEMENT_MISSING_THRESHOLD = 5;

        private readonly MembersListView view;
        private readonly ViewDependencies viewDependencies;
        private readonly IMVCManager mvcManager;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly GenericContextMenu contextMenu;
        private readonly UserProfileContextMenuControlSettings userProfileContextMenuControlSettings;
        private readonly GenericContextMenuElement removeModeratorContextMenuElement;
        private readonly GenericContextMenuElement addModeratorContextMenuElement;
        private readonly GenericContextMenuElement blockUserContextMenuElement;
        private readonly GenericContextMenuElement kickUserContextMenuElement;
        private readonly GenericContextMenuElement banUserContextMenuElement;
        private readonly GenericContextMenuElement communityOptionsSeparatorContextMenuElement;
        private readonly Dictionary<MembersListView.MemberListSections, SectionFetchData> sectionsFetchData = new ()
        {
            { MembersListView.MemberListSections.ALL, new SectionFetchData(PAGE_SIZE) },
            { MembersListView.MemberListSections.BANNED, new SectionFetchData(PAGE_SIZE) }
        };

        private string lastCommunityId = string.Empty;
        private CancellationToken ct;
        private bool isFetching;
        private CommunityMemberRole viewerRole;
        private bool viewerCanEdit => viewerRole is CommunityMemberRole.moderator or CommunityMemberRole.owner;

        private GetCommunityMembersResponse.MemberData lastClickedProfileCtx;
        private CancellationTokenSource friendshipOperationCts = new ();
        private CancellationTokenSource contextMenuOperationCts = new ();
        private UniTaskCompletionSource? panelLifecycleTask;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.ALL;

        public MembersListController(MembersListView view,
            ViewDependencies viewDependencies,
            IMVCManager mvcManager,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider)
        {
            this.view = view;
            this.viewDependencies = viewDependencies;
            this.mvcManager = mvcManager;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;

            contextMenu = new GenericContextMenu(view.ContextMenuSettings.ContextMenuWidth, verticalLayoutPadding: CONTEXT_MENU_VERTICAL_LAYOUT_PADDING, elementsSpacing: CONTEXT_MENU_ELEMENTS_SPACING)
                         .AddControl(userProfileContextMenuControlSettings = new UserProfileContextMenuControlSettings(HandleContextMenuUserProfileButton))
                         .AddControl(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ViewProfileText, view.ContextMenuSettings.ViewProfileSprite, () => OpenProfilePassport(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.ChatText, view.ContextMenuSettings.ChatSprite, () => OpenChatWithUser(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.CallText, view.ContextMenuSettings.CallSprite, () => CallUser(lastClickedProfileCtx!)))
                         .AddControl(blockUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => BlockUserClicked(lastClickedProfileCtx!))))
                         .AddControl(communityOptionsSeparatorContextMenuElement = new GenericContextMenuElement(new SeparatorContextMenuControlSettings(CONTEXT_MENU_SEPARATOR_HEIGHT, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.left, -CONTEXT_MENU_VERTICAL_LAYOUT_PADDING.right)))
                         .AddControl(removeModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.RemoveModeratorText, view.ContextMenuSettings.RemoveModeratorSprite, () => RemoveModerator(lastClickedProfileCtx!))))
                         .AddControl(addModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.AddModeratorText, view.ContextMenuSettings.AddModeratorSprite, () => AddModerator(lastClickedProfileCtx!))))
                         .AddControl(kickUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.KickUserText, view.ContextMenuSettings.KickUserSprite, () => KickUser(lastClickedProfileCtx!))))
                         .AddControl(banUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BanUserText, view.ContextMenuSettings.BanUserSprite, () => BanUser(lastClickedProfileCtx!))));

            this.view.LoopGrid.InitGridView(0, GetLoopGridItemByIndex);
            this.view.ActiveSectionChanged += OnMemberListSectionChanged;
        }

        public void Dispose()
        {
            contextMenuOperationCts.SafeCancelAndDispose();
            friendshipOperationCts.SafeCancelAndDispose();
            view.ActiveSectionChanged -= OnMemberListSectionChanged;
        }

        private void OnMemberListSectionChanged(MembersListView.MemberListSections section)
        {
            if (isFetching)
                WaitForFetchAsync().Forget();
            else
                SwitchSection();

            return;

            async UniTaskVoid WaitForFetchAsync()
            {
                await UniTask.WaitUntil(() => isFetching == false, cancellationToken: ct);
                SwitchSection();
            }

            void SwitchSection()
            {
                currentSection = section;

                SectionFetchData sectionData = sectionsFetchData[section];

                if (sectionData.members.Count == 0 && sectionData.pageNumber == 0)
                    FetchNewDataAsync().Forget();
                else
                    RefreshLoopList();
            }
        }

        private void BlockUserClicked(GetCommunityMembersResponse.MemberData profile) =>
            mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(profile.id), profile.name, BlockUserPromptParams.UserBlockAction.BLOCK)), ct).Forget();

        private void BanUser(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            BanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid BanUserAsync(CancellationToken token)
            {
                try
                {
                    bool result = await communitiesDataProvider.BanUserFromCommunityAsync(profile.id, lastCommunityId, token);

                    if (result)
                    {
                        sectionsFetchData[MembersListView.MemberListSections.ALL].members.Remove(profile);
                        sectionsFetchData[MembersListView.MemberListSections.BANNED].members.Add(profile);
                        RefreshLoopList();
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }

        private void KickUser(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            KickUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid KickUserAsync(CancellationToken token)
            {
                try
                {
                    bool result = await communitiesDataProvider.KickUserFromCommunityAsync(profile.id, lastCommunityId, token);

                    if (result)
                    {
                        sectionsFetchData[MembersListView.MemberListSections.ALL].members.Remove(profile);
                        RefreshLoopList();
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }

        private void AddModerator(GetCommunityMembersResponse.MemberData profile)
        {
            throw new NotImplementedException();
        }

        private void RemoveModerator(GetCommunityMembersResponse.MemberData profile)
        {
            throw new NotImplementedException();
        }

        private void CallUser(GetCommunityMembersResponse.MemberData profile)
        {
            throw new NotImplementedException();
        }

        private void OpenChatWithUser(GetCommunityMembersResponse.MemberData profile)
        {
            throw new NotImplementedException();
        }

        private void OpenProfilePassport(GetCommunityMembersResponse.MemberData profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.id)), ct).Forget();

        public void Reset()
        {
            lastCommunityId = string.Empty;

            foreach (var element in sectionsFetchData)
                element.Value.Reset();

            isFetching = false;
            panelLifecycleTask?.TrySetResult();
        }

        private void HandleContextMenuUserProfileButton(UserProfileContextMenuControlSettings.UserData userData, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            switch (friendshipStatus)
            {
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT:
                    CancelFriendshipRequestAsync(friendshipOperationCts.Token).Forget();
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED:
                    mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                    {
                        OneShotFriendAccepted = userData.ToFriendProfile()
                    }), ct: friendshipOperationCts.Token).Forget();
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED:
                    mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(userData.userAddress), userData.userName, BlockUserPromptParams.UserBlockAction.UNBLOCK)), ct).Forget();
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND:
                    mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
                    {
                        UserId = new Web3Address(userData.userAddress),
                    }), friendshipOperationCts.Token);
                    break;
                case UserProfileContextMenuControlSettings.FriendshipStatus.NONE:
                    mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                    {
                        DestinationUser = new Web3Address(userData.userAddress),
                    }), friendshipOperationCts.Token);
                    break;
            }

            return;

            async UniTaskVoid CancelFriendshipRequestAsync(CancellationToken ct)
            {
                try
                {
                    await friendServiceProxy.StrictObject.CancelFriendshipAsync(userData.userAddress, ct);
                }
                catch(Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }

        private LoopGridViewItem GetLoopGridItemByIndex(LoopGridView loopGridView, int index, int row, int column)
        {
            LoopGridViewItem listItem = loopGridView.NewListViewItem(loopGridView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListItemView elementView = listItem.GetComponent<MemberListItemView>();

            elementView.InjectDependencies(viewDependencies);
            elementView.Configure(sectionsFetchData[currentSection].members[index], currentSection);

            elementView.SubscribeToInteractions(MainButtonClicked, ContextMenuButtonClicked, FriendButtonClicked, UnbanButtonClicked);

            if (index >= sectionsFetchData[currentSection].totalFetched - ELEMENT_MISSING_THRESHOLD && sectionsFetchData[currentSection].totalFetched < sectionsFetchData[currentSection].totalToFetch && !isFetching)
                FetchNewDataAsync().Forget();

            return listItem;
        }

        private async UniTaskVoid FetchNewDataAsync()
        {
            isFetching = true;

            sectionsFetchData[currentSection].pageNumber++;
            await FetchDataAsync();
            sectionsFetchData[currentSection].totalFetched = (sectionsFetchData[currentSection].pageNumber + 1) * PAGE_SIZE;

            RefreshLoopList();

            isFetching = false;
        }

        private void RefreshLoopList()
        {
            view.LoopGrid.SetListItemCount(sectionsFetchData[currentSection].members.Count, false);
            view.LoopGrid.RefreshAllShownItem();
        }

        private async UniTask FetchDataAsync()
        {
            GetCommunityMembersResponse response = await communitiesDataProvider.GetCommunityMembersAsync(lastCommunityId, currentSection == MembersListView.MemberListSections.BANNED, sectionsFetchData[currentSection].pageNumber, PAGE_SIZE, ct);
            sectionsFetchData[currentSection].members.AddRange(response.members);
            sectionsFetchData[currentSection].totalToFetch = response.totalAmount;
        }

        public void ShowMembersListAsync(string communityId, CommunityMemberRole userRole, CancellationToken cancellationToken)
        {
            ct = cancellationToken;

            if (lastCommunityId == null || lastCommunityId.Equals(communityId)) return;

            lastCommunityId = communityId;
            viewerRole = userRole;
            view.SetSectionButtonsActive(viewerRole is CommunityMemberRole.moderator or CommunityMemberRole.owner);
            panelLifecycleTask = new UniTaskCompletionSource();

            FetchNewDataAsync().Forget();
        }

        private void MainButtonClicked(GetCommunityMembersResponse.MemberData profile)
        {
            // Handle main button click
            Debug.Log("MainButtonClicked: " + profile.id);
        }

        private static UserProfileContextMenuControlSettings.FriendshipStatus ConvertFriendshipStatus(FriendshipStatus status)
        {
            return status switch
            {
                FriendshipStatus.friend => UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND,
                FriendshipStatus.request_received => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED,
                FriendshipStatus.request_sent => UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT,
                FriendshipStatus.blocked => UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED,
                FriendshipStatus.blocked_by => UserProfileContextMenuControlSettings.FriendshipStatus.DISABLED,
                FriendshipStatus.none => UserProfileContextMenuControlSettings.FriendshipStatus.NONE,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }

        private void ContextMenuButtonClicked(GetCommunityMembersResponse.MemberData profile, Vector2 buttonPosition, MemberListItemView elementView)
        {
            lastClickedProfileCtx = profile;
            userProfileContextMenuControlSettings.SetInitialData(profile.ToUserData(), ConvertFriendshipStatus(profile.friendshipStatus));
            elementView.CanUnHover = false;

            removeModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.moderator && viewerCanEdit;
            addModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.member && viewerCanEdit;
            blockUserContextMenuElement.Enabled = profile.friendshipStatus != FriendshipStatus.blocked && profile.friendshipStatus != FriendshipStatus.blocked_by;
            kickUserContextMenuElement.Enabled = viewerCanEdit && currentSection == MembersListView.MemberListSections.ALL;
            banUserContextMenuElement.Enabled = viewerCanEdit && currentSection == MembersListView.MemberListSections.ALL;

            communityOptionsSeparatorContextMenuElement.Enabled = removeModeratorContextMenuElement.Enabled || addModeratorContextMenuElement.Enabled || kickUserContextMenuElement.Enabled || banUserContextMenuElement.Enabled;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition,
                           actionOnHide: () => elementView.CanUnHover = true,
                           closeTask: panelLifecycleTask?.Task)), ct)
                      .Forget();
        }

        private void FriendButtonClicked(GetCommunityMembersResponse.MemberData profile) =>
            HandleContextMenuUserProfileButton(profile.ToUserData(), ConvertFriendshipStatus(profile.friendshipStatus));

        private void UnbanButtonClicked(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            UnbanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid UnbanUserAsync(CancellationToken token)
            {
                try
                {
                    bool result = await communitiesDataProvider.UnBanUserFromCommunityAsync(profile.id, lastCommunityId, token);

                    if (result)
                    {
                        sectionsFetchData[MembersListView.MemberListSections.BANNED].members.Remove(profile);
                        RefreshLoopList();
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }
    }
}
