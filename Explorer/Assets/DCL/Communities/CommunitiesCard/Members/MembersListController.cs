using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Friends;
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
        private readonly Dictionary<MembersListView.MemberListSections, SectionFetchData> sectionsFetchData = new ()
        {
            { MembersListView.MemberListSections.ALL, new SectionFetchData(PAGE_SIZE) },
            { MembersListView.MemberListSections.BANNED, new SectionFetchData(PAGE_SIZE) }
        };

        private string lastCommunityId = string.Empty;
        private CancellationToken ct;
        private bool isFetching;
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
                         .AddControl(removeModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.RemoveModeratorText, view.ContextMenuSettings.RemoveModeratorSprite, () => RemoveModerator(lastClickedProfileCtx!))))
                         .AddControl(addModeratorContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.AddModeratorText, view.ContextMenuSettings.AddModeratorSprite, () => AddModerator(lastClickedProfileCtx!))))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.KickUserText, view.ContextMenuSettings.KickUserSprite, () => KickUser(lastClickedProfileCtx!)))
                         .AddControl(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BanUserText, view.ContextMenuSettings.BanUserSprite, () => BanUser(lastClickedProfileCtx!)))
                         .AddControl(blockUserContextMenuElement = new GenericContextMenuElement(new ButtonContextMenuControlSettings(view.ContextMenuSettings.BlockText, view.ContextMenuSettings.BlockSprite, () => BlockUserClicked(lastClickedProfileCtx!))));

            this.view.LoopList.InitListView(0, GetLoopListItemByIndex);
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
                    await communitiesDataProvider.BanUserFromCommunityAsync(profile.id, lastCommunityId, token);
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
                    await communitiesDataProvider.KickUserFromCommunityAsync(profile.id, lastCommunityId, token);
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

        private void HandleContextMenuUserProfileButton(string userId, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            friendshipOperationCts = friendshipOperationCts.SafeRestart();

            if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT)
                CancelFriendshipRequestAsync(friendshipOperationCts.Token).Forget();
            else if (friendshipStatus == UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED)
                //TODO: create friendship request
                mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams { OneShotFriendAccepted = null }), ct: friendshipOperationCts.Token).Forget();

            return;

            async UniTaskVoid CancelFriendshipRequestAsync(CancellationToken ct)
            {
                try
                {
                    await friendServiceProxy.StrictObject.CancelFriendshipAsync(userId, ct);
                }
                catch(Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }

        private LoopListViewItem2 GetLoopListItemByIndex(LoopListView2 loopListView, int index)
        {
            LoopListViewItem2 listItem = loopListView.NewListViewItem(loopListView.ItemPrefabDataList[0].mItemPrefab.name);
            MemberListRowItemView elementView = listItem.GetComponent<MemberListRowItemView>();

            elementView.ResetElements();

            int leftIndex = index * 2;
            int rightIndex = leftIndex + 1;

            if (rightIndex < sectionsFetchData[currentSection].members.Count)
                elementView.ConfigureRight(sectionsFetchData[currentSection].members[rightIndex], viewDependencies);

            elementView.ConfigureLeft(sectionsFetchData[currentSection].members[leftIndex], viewDependencies);

            elementView.SubscribeToInteractions(MainButtonClicked, ContextMenuButtonClicked, FriendButtonClicked);

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
            view.LoopList.SetListItemCount(Mathf.CeilToInt(sectionsFetchData[currentSection].members.Count * 1f / 2), false);
            view.LoopList.RefreshAllShownItem();
        }

        private async UniTask FetchDataAsync()
        {
            GetCommunityMembersResponse response = await communitiesDataProvider.GetCommunityMembersAsync(lastCommunityId, currentSection == MembersListView.MemberListSections.BANNED, sectionsFetchData[currentSection].pageNumber, PAGE_SIZE, ct);
            sectionsFetchData[currentSection].members.AddRange(response.members);
            sectionsFetchData[currentSection].totalToFetch = response.totalPages * PAGE_SIZE;
        }

        public void ShowMembersListAsync(string communityId, CancellationToken cancellationToken)
        {
            if (lastCommunityId == null || lastCommunityId.Equals(communityId)) return;

            ct = cancellationToken;
            lastCommunityId = communityId;
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

        private void ContextMenuButtonClicked(GetCommunityMembersResponse.MemberData profile, Vector2 buttonPosition, MemberListSingleItemView elementView)
        {
            lastClickedProfileCtx = profile;
            userProfileContextMenuControlSettings.SetInitialData(profile.name, profile.id, profile.hasClaimedName,
                profile.UserNameColor,
                ConvertFriendshipStatus(profile.friendshipStatus),
                profile.profilePicture);
            elementView.CanUnHover = false;

            removeModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.moderator;
            addModeratorContextMenuElement.Enabled = profile.role == CommunityMemberRole.member;
            blockUserContextMenuElement.Enabled = profile.friendshipStatus != FriendshipStatus.blocked && profile.friendshipStatus != FriendshipStatus.blocked_by;

            mvcManager.ShowAsync(GenericContextMenuController.IssueCommand(new GenericContextMenuParameter(contextMenu, buttonPosition,
                           actionOnHide: () => elementView.CanUnHover = true,
                           closeTask: panelLifecycleTask?.Task)), ct)
                      .Forget();
        }

        private void FriendButtonClicked(GetCommunityMembersResponse.MemberData profile) =>
            HandleContextMenuUserProfileButton(profile.id, ConvertFriendshipStatus(profile.friendshipStatus));
    }
}
