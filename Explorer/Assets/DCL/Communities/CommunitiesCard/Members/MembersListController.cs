using Cysharp.Threading.Tasks;
using DCL.Chat.ControllerShowParams;
using DCL.Chat.EventBus;
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
using Utility;
using Utility.Types;
using MemberData = DCL.Communities.GetCommunityMembersResponse.MemberData;

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
        private const int WARNING_NOTIFICATION_DURATION_MS = 3000;

        private readonly MembersListView view;
        private readonly ConfirmationDialogView confirmationDialogView;
        private readonly IMVCManager mvcManager;
        private readonly ObjectProxy<IFriendsService> friendServiceProxy;
        private readonly ICommunitiesDataProvider communitiesDataProvider;
        private readonly WarningNotificationView inWorldWarningNotificationView;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly IChatEventBus chatEventBus;

        private readonly SectionFetchData<MemberData> allMembersFetchData = new (PAGE_SIZE);
        private readonly SectionFetchData<MemberData> bannedMembersFetchData = new (PAGE_SIZE);

        private GetCommunityResponse.CommunityData? communityData = null;
        protected override SectionFetchData<MemberData> currentSectionFetchData => currentSection == MembersListView.MemberListSections.ALL ? allMembersFetchData : bannedMembersFetchData;

        private CancellationTokenSource friendshipOperationCts = new ();
        private CancellationTokenSource contextMenuOperationCts = new ();
        private UniTaskCompletionSource? panelLifecycleTask;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.ALL;

        public MembersListController(MembersListView view,
            ProfileRepositoryWrapper profileDataProvider,
            IMVCManager mvcManager,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider,
            WarningNotificationView inWorldWarningNotificationView,
            IWeb3IdentityCache web3IdentityCache,
            ISharedSpaceManager sharedSpaceManager,
            IChatEventBus chatEventBus) : base(view, PAGE_SIZE)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.friendServiceProxy = friendServiceProxy;
            this.communitiesDataProvider = communitiesDataProvider;
            this.inWorldWarningNotificationView = inWorldWarningNotificationView;
            this.sharedSpaceManager = sharedSpaceManager;
            this.chatEventBus = chatEventBus;

            this.view.InitGrid(() => currentSectionFetchData, web3IdentityCache, mvcManager);
            this.view.ActiveSectionChanged += OnMemberListSectionChanged;
            this.view.ElementMainButtonClicked += OnMainButtonClicked;
            this.view.ContextMenuUserProfileButtonClicked += HandleContextMenuUserProfileButtonAsync;
            this.view.ElementFriendButtonClicked += OnFriendButtonClicked;
            this.view.ElementUnbanButtonClicked += OnUnbanButtonClicked;

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
            view.ActiveSectionChanged -= OnMemberListSectionChanged;
            view.ElementMainButtonClicked -= OnMainButtonClicked;
            view.ContextMenuUserProfileButtonClicked -= HandleContextMenuUserProfileButtonAsync;
            view.ElementFriendButtonClicked -= OnFriendButtonClicked;
            view.ElementUnbanButtonClicked -= OnUnbanButtonClicked;

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

                if (sectionData.pageNumber == 0)
                    FetchNewDataAsync(cancellationToken).Forget();
                else
                    view.RefreshGrid();
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

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(BAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token);
                    return;
                }

                allMembersFetchData.items.Remove(profile);

                List<MemberData> memberList = bannedMembersFetchData.items;
                profile.role = CommunityMemberRole.none;
                memberList.Add(profile);

                MembersSorter.SortMembersList(memberList);

                view.RefreshGrid();
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

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(KICK_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token);
                    return;
                }

                allMembersFetchData.items.Remove(profile);
                view.RefreshGrid();
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

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(ADD_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token);
                    return;
                }

                List<MemberData> memberList = allMembersFetchData.items;

                foreach (MemberData member in memberList)
                    if (member.memberAddress.Equals(profile.memberAddress))
                    {
                        member.role = CommunityMemberRole.moderator;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                view.RefreshGrid();
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

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(REMOVE_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token);
                    return;
                }

                List<MemberData> memberList = allMembersFetchData.items;
                foreach (MemberData member in memberList)
                    if (member.memberAddress.Equals(profile.memberAddress))
                    {
                        member.role = CommunityMemberRole.member;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                view.RefreshGrid();
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

            panelLifecycleTask?.TrySetResult();

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

            currentSectionFetchData.items.Find(item => item.memberAddress.Equals(userId))
                                   .friendshipStatus = status.Convert();

            view.RefreshGrid();
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            SectionFetchData<MemberData> membersData = currentSectionFetchData;

            Result<GetCommunityMembersResponse> response = currentSection == MembersListView.MemberListSections.ALL
                ? await communitiesDataProvider.GetCommunityMembersAsync(communityData?.id, membersData.pageNumber, PAGE_SIZE, ct)
                                               .SuppressToResultAsync(ReportCategory.COMMUNITIES)
                : await communitiesDataProvider.GetBannedCommunityMembersAsync(communityData?.id, membersData.pageNumber, PAGE_SIZE, ct)
                                               .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!response.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                membersData.pageNumber--;
                return membersData.totalToFetch;
            }

            foreach (var member in response.Value.data.results)
                if (!membersData.items.Contains(member))
                    membersData.items.Add(member);

            MembersSorter.SortMembersList(membersData.items);

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
        }

        private void OnMainButtonClicked(MemberData profile)
        {
            // Handle main button click
            // Debug.Log("MainButtonClicked: " + profile.id);
        }

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

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(UNBAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct);
                    return;
                }

                bannedMembersFetchData.items.Remove(profile);
                view.RefreshGrid();

            }
        }
    }
}
