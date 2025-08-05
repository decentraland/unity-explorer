using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
using DCL.Communities.CommunitiesDataProvider.DTOs;
using DCL.Diagnostics;
using DCL.Friends;
using DCL.Friends.UI;
using DCL.Friends.UI.BlockUserPrompt;
using DCL.Friends.UI.Requests;
using DCL.Passport;
using DCL.UI;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;
using MemberData = DCL.Communities.CommunitiesDataProvider.DTOs.GetCommunityMembersResponse.MemberData;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : CommunityFetchingControllerBase<MemberData, MembersListView>
    {
        private const int PAGE_SIZE = 20;
        private const string UNBAN_USER_ERROR_TEXT = "There was an error unbanning the user. Please try again.";
        private const string REMOVE_MODERATOR_ERROR_TEXT = "There was an error removing moderator from user. Please try again.";
        private const string ADD_MODERATOR_ERROR_TEXT = "There was an error adding moderator to user. Please try again.";
        private const string KICK_USER_ERROR_TEXT = "There was an error kicking the user. Please try again.";
        private const string BAN_USER_ERROR_TEXT = "There was an error banning the user. Please try again.";
        private const string MANAGE_REQUEST_ERROR_TEXT = "There was an error managing the user request. Please try again.";
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        private readonly MembersListView view;
        private readonly IMVCManager mvcManager;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private readonly SectionFetchData<MemberData> allMembersFetchData = new (PAGE_SIZE);
        private readonly SectionFetchData<MemberData> bannedMembersFetchData = new (PAGE_SIZE);
        private readonly SectionFetchData<MemberData> requestingMembersFetchData = new (PAGE_SIZE);
        private readonly SectionFetchData<MemberData> invitedMembersFetchData = new (PAGE_SIZE);

        private GetCommunityResponse.CommunityData? communityData = null;

        private int requestAmount;
        private int RequestsAmount
        {
            get => requestAmount;

            set
            {
                requestAmount = value;
                view.UpdateRequestsCounter(value);
            }
        }
        protected override SectionFetchData<MemberData> currentSectionFetchData
        {
            get
            {
                switch (currentSection)
                {
                    case MembersListView.MemberListSections.MEMBERS:
                        return allMembersFetchData;
                    case MembersListView.MemberListSections.BANNED:
                        return bannedMembersFetchData;
                    case MembersListView.MemberListSections.REQUESTS:
                        return requestingMembersFetchData;
                    case MembersListView.MemberListSections.INVITES:
                        return invitedMembersFetchData;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(currentSection), currentSection, null);
                }
            }
        }

        private CancellationTokenSource friendshipOperationCts = new ();
        private CancellationTokenSource contextMenuOperationCts = new ();
        private CancellationTokenSource communityOperationCts = new ();
        private UniTaskCompletionSource? panelLifecycleTask;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.MEMBERS;

        public MembersListController(MembersListView view,
            ProfileRepositoryWrapper profileDataProvider,
            IMVCManager mvcManager,
            ObjectProxy<IFriendsService> friendServiceProxy,
            CommunitiesDataProvider.CommunitiesDataProvider communitiesDataProvider,
            WarningNotificationView inWorldWarningNotificationView,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus,
            IWeb3IdentityCache web3IdentityCache) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;
            this.web3IdentityCache = web3IdentityCache;

            this.view.InitGrid();
            this.view.ActiveSectionChanged += OnMemberListSectionChanged;
            this.view.ElementMainButtonClicked += OnMainButtonClicked;
            this.view.ContextMenuUserProfileButtonClicked += HandleContextMenuUserProfileButtonAsync;
            this.view.ElementFriendButtonClicked += OnFriendButtonClicked;
            this.view.ElementUnbanButtonClicked += OnUnbanButtonClicked;
            this.view.ElementManageRequestClicked += OnManageRequestClicked;

            this.view.OpenProfilePassportRequested += OpenProfilePassport;
            this.view.OpenUserChatRequested += OpenChatWithUserAsync;
            this.view.CallUserRequested += CallUser;
            this.view.BlockUserRequested += BlockUserClickedAsync;
            this.view.RemoveModeratorRequested += RemoveModerator;
            this.view.AddModeratorRequested += AddModerator;
            this.view.KickUserRequested += OnKickUser;
            this.view.BanUserRequested += OnBanUser;

            this.view.SetProfileDataProvider(profileDataProvider);
        }

        public override void Dispose()
        {
            contextMenuOperationCts.SafeCancelAndDispose();
            friendshipOperationCts.SafeCancelAndDispose();
            communityOperationCts.SafeCancelAndDispose();
            view.ActiveSectionChanged -= OnMemberListSectionChanged;
            view.ElementMainButtonClicked -= OnMainButtonClicked;
            view.ContextMenuUserProfileButtonClicked -= HandleContextMenuUserProfileButtonAsync;
            view.ElementFriendButtonClicked -= OnFriendButtonClicked;
            view.ElementUnbanButtonClicked -= OnUnbanButtonClicked;
            view.ElementManageRequestClicked -= OnManageRequestClicked;

            view.OpenProfilePassportRequested -= OpenProfilePassport;
            view.OpenUserChatRequested -= OpenChatWithUserAsync;
            view.CallUserRequested -= CallUser;
            view.BlockUserRequested -= BlockUserClickedAsync;
            view.RemoveModeratorRequested -= RemoveModerator;
            view.AddModeratorRequested -= AddModerator;
            view.KickUserRequested -= OnKickUser;
            view.BanUserRequested -= OnBanUser;

            base.Dispose();
        }

        private void OnManageRequestClicked(MemberData profile, InviteRequestIntention intention)
        {
            communityOperationCts = communityOperationCts.SafeRestart();
            ManageRequestAsync(communityOperationCts.Token).Forget();
            return;

            async UniTaskVoid ManageRequestAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.ManageInviteRequestToJoinAsync(communityData!.Value.id, profile.memberAddress, intention, ct)
                                                                   .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(MANAGE_REQUEST_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                RequestsAmount--;
                currentSectionFetchData.Items.Remove(profile);

                if (intention == InviteRequestIntention.accept)
                {
                    profile.role = CommunityMemberRole.member;
                    allMembersFetchData.Items.Add(profile);
                    MembersSorter.SortMembersList(allMembersFetchData.Items);
                }

                RefreshGrid(true);
            }

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
                await UniTask.WaitUntil(() => isFetching == false, cancellationToken: cancellationToken);
                SwitchSection();
            }

            void SwitchSection()
            {
                currentSection = section;

                SectionFetchData<MemberData> sectionData = currentSectionFetchData;

                if (sectionData.PageNumber == 0)
                    FetchNewDataAsync(cancellationToken).Forget();
                else
                    view.SetEmptyStateActive(sectionData.Items.Count == 0);

                RefreshGrid(true);
            }
        }

        private async void BlockUserClickedAsync(MemberData profile)
        {
            try
            {
                await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(
                    new BlockUserPromptParams(new Web3Address(profile.memberAddress), profile.name, BlockUserPromptParams.UserBlockAction.BLOCK)),
                    cancellationToken);
                await FetchFriendshipStatusAndRefreshAsync(profile.memberAddress, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void OnBanUser(MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            BanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid BanUserAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.BanUserFromCommunityAsync(profile.memberAddress, communityData?.id, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(BAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                allMembersFetchData.Items.Remove(profile);

                List<MemberData> memberList = bannedMembersFetchData.Items;
                profile.role = CommunityMemberRole.none;
                memberList.Add(profile);

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void OnKickUser(MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            KickUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid KickUserAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.KickUserFromCommunityAsync(profile.memberAddress, communityData?.id, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(KICK_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                allMembersFetchData.Items.Remove(profile);
                RefreshGrid(true);
            }
        }

        public void TryRemoveLocalUser()
        {
            string? userAddress = web3IdentityCache.Identity?.Address;
            for (int i = 0; i < allMembersFetchData.Items.Count; i++)
                if (allMembersFetchData.Items[i].memberAddress.Equals(userAddress, StringComparison.OrdinalIgnoreCase))
                {
                    allMembersFetchData.Items.RemoveAt(i);
                    if (currentSection == MembersListView.MemberListSections.MEMBERS)
                        RefreshGrid(true);
                    break;
                }
        }

        private void AddModerator(MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            AddModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid AddModeratorAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.SetMemberRoleAsync(profile.memberAddress, communityData?.id, CommunityMemberRole.moderator, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(ADD_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                List<MemberData> memberList = allMembersFetchData.Items;

                foreach (MemberData member in memberList)
                    if (member.memberAddress.Equals(profile.memberAddress))
                    {
                        member.role = CommunityMemberRole.moderator;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void RemoveModerator(MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            RemoveModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid RemoveModeratorAsync(CancellationToken token)
            {

                Result<bool> result = await communitiesDataProvider.SetMemberRoleAsync(profile.memberAddress, communityData?.id, CommunityMemberRole.member, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(REMOVE_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                List<MemberData> memberList = allMembersFetchData.Items;
                foreach (MemberData member in memberList)
                    if (member.memberAddress.Equals(profile.memberAddress))
                    {
                        member.role = CommunityMemberRole.member;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void CallUser(MemberData profile)
        {
            //TODO: call user in private conversation
            throw new NotImplementedException();
        }

        private async void OpenChatWithUserAsync(MemberData profile)
        {
            try
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
                chatEventBus.OpenPrivateConversationUsingUserId(profile.memberAddress);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void OpenProfilePassport(MemberData profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.memberAddress)), cancellationToken).Forget();

        public override void Reset()
        {
            communityData = null;

            allMembersFetchData.Reset();
            bannedMembersFetchData.Reset();
            requestingMembersFetchData.Reset();

            panelLifecycleTask?.TrySetResult();

            RequestsAmount = 0;

            base.Reset();
        }

        private async void HandleContextMenuUserProfileButtonAsync(UserProfileContextMenuControlSettings.UserData userData, UserProfileContextMenuControlSettings.FriendshipStatus friendshipStatus)
        {
            try
            {
                friendshipOperationCts = friendshipOperationCts.SafeRestart();
                CancellationToken ct = friendshipOperationCts.Token;

                switch (friendshipStatus)
                {
                    case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_SENT:
                        await friendServiceProxy.StrictObject.CancelFriendshipAsync(userData.userAddress, ct)
                                                .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                        break;
                    case UserProfileContextMenuControlSettings.FriendshipStatus.REQUEST_RECEIVED:
                        await mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                        {
                            OneShotFriendAccepted = userData.ToFriendProfile()
                        }), ct: ct);

                        break;
                    case UserProfileContextMenuControlSettings.FriendshipStatus.BLOCKED:
                        await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(
                            new Web3Address(userData.userAddress), userData.userName, BlockUserPromptParams.UserBlockAction.UNBLOCK)),
                            ct);
                        break;
                    case UserProfileContextMenuControlSettings.FriendshipStatus.FRIEND:
                        await mvcManager.ShowAsync(UnfriendConfirmationPopupController.IssueCommand(new UnfriendConfirmationPopupController.Params
                        {
                            UserId = new Web3Address(userData.userAddress),
                        }), ct);

                        break;
                    case UserProfileContextMenuControlSettings.FriendshipStatus.NONE:
                        await mvcManager.ShowAsync(FriendRequestController.IssueCommand(new FriendRequestParams
                        {
                            DestinationUser = new Web3Address(userData.userAddress),
                        }), ct);

                        break;
                }

                if (ct.IsCancellationRequested)
                    return;

                await FetchFriendshipStatusAndRefreshAsync(userData.userAddress, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private async UniTask FetchFriendshipStatusAndRefreshAsync(string userId, CancellationToken ct)
        {
            Friends.FriendshipStatus status = await friendServiceProxy.StrictObject.GetFriendshipStatusAsync(userId, ct);

            currentSectionFetchData.Items.Find(item => item.memberAddress.Equals(userId))
                                   .friendshipStatus = status.Convert();

            RefreshGrid(true);
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            SectionFetchData<MemberData> membersData = currentSectionFetchData;

            UniTask<GetCommunityMembersResponse> responseTask;
            switch (currentSection)
            {
                case MembersListView.MemberListSections.MEMBERS:
                    responseTask = communitiesDataProvider.GetCommunityMembersAsync(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct);
                    break;
                case MembersListView.MemberListSections.BANNED:
                    responseTask = communitiesDataProvider.GetBannedCommunityMembersAsync(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct);
                    break;
                case MembersListView.MemberListSections.REQUESTS:
                    //communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.request, membersData.PageNumber, PAGE_SIZE, ct);
                    responseTask = communitiesDataProvider.GetCommunityRequestsToJoin(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct);
                    break;
                case MembersListView.MemberListSections.INVITES:
                    //communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.invite, membersData.PageNumber, PAGE_SIZE, ct);
                    responseTask = communitiesDataProvider.GetCommunityInvitesToJoin(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(currentSection), currentSection, null);
            }

            Result<GetCommunityMembersResponse> response = await responseTask.SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!response.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                membersData.PageNumber--;
                return membersData.TotalToFetch;
            }

            foreach (var member in response.Value.data.results)
                if (!membersData.Items.Contains(member))
                    membersData.Items.Add(member);

            MembersSorter.SortMembersList(membersData.Items);

            return response.Value.data.total;
        }

        public void ShowMembersList(GetCommunityResponse.CommunityData community, CancellationToken ct)
        {
            cancellationToken = ct;

            if (communityData is not null && community.id.Equals(communityData.Value.id)) return;

            communityData = community;
            view.SetSectionButtonsActive(communityData?.role is CommunityMemberRole.moderator or CommunityMemberRole.owner);
            panelLifecycleTask = new UniTaskCompletionSource();
            view.SetCommunityData(community, panelLifecycleTask!.Task, ct);

            FetchNewDataAsync(ct).Forget();
            FetchRequestsToJoinAsync(ct).Forget();
        }

        private async UniTaskVoid FetchRequestsToJoinAsync(CancellationToken ct)
        {
            Result<GetCommunityMembersResponse> response = await communitiesDataProvider.GetCommunityRequestsToJoin(communityData?.id, 1, 0, ct)
                                                                                          .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return;

            if (!response.Success)
                return;

            RequestsAmount = response.Value.data.total;
        }

        private void OnMainButtonClicked(MemberData profile) =>
            OpenProfilePassport(profile);

        private void OnFriendButtonClicked(MemberData profile) =>
            HandleContextMenuUserProfileButtonAsync(profile.ToUserData(), profile.friendshipStatus.Convert());

        private void OnUnbanButtonClicked(MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            UnbanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid UnbanUserAsync(CancellationToken ct)
            {

                Result<bool> result = await communitiesDataProvider.UnBanUserFromCommunityAsync(profile.memberAddress, communityData?.id, ct)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(UNBAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                bannedMembersFetchData.Items.Remove(profile);
                RefreshGrid(false);
            }
        }
    }
}
