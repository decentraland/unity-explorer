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
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : CommunityFetchingControllerBase<ICommunityMemberData, MembersListView>
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

        private readonly Dictionary<MembersListView.MemberListSections, SectionFetchData<ICommunityMemberData>> sectionsFetchData = new ();

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
        protected override SectionFetchData<ICommunityMemberData> currentSectionFetchData => sectionsFetchData[currentSection];

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

            foreach (MembersListView.MemberListSections section in EnumUtils.Values<MembersListView.MemberListSections>())
                sectionsFetchData[section] = new SectionFetchData<ICommunityMemberData>(PAGE_SIZE);
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

        private void OnManageRequestClicked(ICommunityMemberData profile, InviteRequestIntention intention)
        {
            communityOperationCts = communityOperationCts.SafeRestart();
            ManageRequestAsync(communityOperationCts.Token).Forget();
            return;

            async UniTaskVoid ManageRequestAsync(CancellationToken ct)
            {
                Result<bool> result = await communitiesDataProvider.ManageInviteRequestToJoinAsync(communityData!.Value.id, profile.Id, intention, ct)
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
                    profile.Role = CommunityMemberRole.member;
                    List<ICommunityMemberData> memberList = sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items;
                    memberList.Add(profile);
                    MembersSorter.SortMembersList(memberList);
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

                SectionFetchData<ICommunityMemberData> sectionData = currentSectionFetchData;

                if (sectionData.PageNumber == 0)
                    FetchNewDataAsync(cancellationToken).Forget();
                else
                    view.SetEmptyStateActive(sectionData.Items.Count == 0);

                RefreshGrid(true);
            }
        }

        private async void BlockUserClickedAsync(ICommunityMemberData profile)
        {
            try
            {
                await mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(
                    new BlockUserPromptParams(new Web3Address(profile.Address), profile.Name, BlockUserPromptParams.UserBlockAction.BLOCK)),
                    cancellationToken);
                await FetchFriendshipStatusAndRefreshAsync(profile.Address, cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void OnBanUser(ICommunityMemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            BanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid BanUserAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.BanUserFromCommunityAsync(profile.Address, communityData?.id, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(BAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items.Remove(profile);

                List<ICommunityMemberData> memberList = sectionsFetchData[MembersListView.MemberListSections.BANNED].Items;
                profile.Role = CommunityMemberRole.none;
                memberList.Add(profile);

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void OnKickUser(ICommunityMemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            KickUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid KickUserAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.KickUserFromCommunityAsync(profile.Address, communityData?.id, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(KICK_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items.Remove(profile);
                RefreshGrid(true);
            }
        }

        public void TryRemoveLocalUser()
        {
            List<ICommunityMemberData> memberList = sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items;

            string? userAddress = web3IdentityCache.Identity?.Address;
            for (int i = 0; i < memberList.Count; i++)
                if (memberList[i].Address.Equals(userAddress, StringComparison.OrdinalIgnoreCase))
                {
                    memberList.RemoveAt(i);
                    if (currentSection == MembersListView.MemberListSections.MEMBERS)
                        RefreshGrid(true);
                    break;
                }
        }

        private void AddModerator(ICommunityMemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            AddModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid AddModeratorAsync(CancellationToken token)
            {
                Result<bool> result = await communitiesDataProvider.SetMemberRoleAsync(profile.Address, communityData?.id, CommunityMemberRole.moderator, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(ADD_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                List<ICommunityMemberData> memberList = sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items;

                foreach (ICommunityMemberData member in memberList)
                    if (member.Address.Equals(profile.Address))
                    {
                        member.Role = CommunityMemberRole.moderator;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void RemoveModerator(ICommunityMemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            RemoveModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid RemoveModeratorAsync(CancellationToken token)
            {

                Result<bool> result = await communitiesDataProvider.SetMemberRoleAsync(profile.Address, communityData?.id, CommunityMemberRole.member, token)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (token.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(REMOVE_MODERATOR_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, token)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                List<ICommunityMemberData> memberList = sectionsFetchData[MembersListView.MemberListSections.MEMBERS].Items;
                foreach (ICommunityMemberData member in memberList)
                    if (member.Address.Equals(profile.Address))
                    {
                        member.Role = CommunityMemberRole.member;
                        break;
                    }

                MembersSorter.SortMembersList(memberList);

                RefreshGrid(true);
            }
        }

        private void CallUser(ICommunityMemberData profile)
        {
            //TODO: call user in private conversation
            throw new NotImplementedException();
        }

        private async void OpenChatWithUserAsync(ICommunityMemberData profile)
        {
            try
            {
                await sharedSpaceManager.ShowAsync(PanelsSharingSpace.Chat, new ChatControllerShowParams(true, true));
                chatEventBus.OpenPrivateConversationUsingUserId(profile.Address);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.COMMUNITIES);
            }
        }

        private void OpenProfilePassport(ICommunityMemberData profile) =>
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.Address)), cancellationToken).Forget();

        public override void Reset()
        {
            communityData = null;
            currentSection = MembersListView.MemberListSections.MEMBERS;
            view.Close();

            foreach (var sectionFetchData in sectionsFetchData)
                sectionFetchData.Value.Reset();

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

            currentSectionFetchData.Items.Find(item => item.Address.Equals(userId))
                                   .FriendshipStatus = status.Convert();

            RefreshGrid(true);
        }

        protected override async UniTask<int> FetchDataAsync(CancellationToken ct)
        {
            SectionFetchData<ICommunityMemberData> membersData = currentSectionFetchData;

            UniTask<ICommunityMemberPagedResponse> responseTask = currentSection switch
                                                                  {
                                                                      MembersListView.MemberListSections.MEMBERS => communitiesDataProvider.GetCommunityMembersAsync(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct),
                                                                      MembersListView.MemberListSections.BANNED => communitiesDataProvider.GetBannedCommunityMembersAsync(communityData?.id, membersData.PageNumber, PAGE_SIZE, ct),
                                                                      MembersListView.MemberListSections.REQUESTS =>
                                                                          communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.request, membersData.PageNumber, PAGE_SIZE, ct),
                                                                      MembersListView.MemberListSections.INVITES =>
                                                                          communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.invite, membersData.PageNumber, PAGE_SIZE, ct),
                                                                      _ => throw new ArgumentOutOfRangeException(nameof(currentSection), currentSection, null)
                                                                  };

            Result<ICommunityMemberPagedResponse> response = await responseTask.SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (ct.IsCancellationRequested)
                return 0;

            if (!response.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                membersData.PageNumber--;
                return membersData.TotalToFetch;
            }

            foreach (var member in response.Value.members)
                if (!membersData.Items.Contains(member))
                    membersData.Items.Add(member);

            MembersSorter.SortMembersList(membersData.Items);

            if (currentSection == MembersListView.MemberListSections.REQUESTS)
                RequestsAmount = response.Value.total;

            return response.Value.total;
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
            return;

            async UniTaskVoid FetchRequestsToJoinAsync(CancellationToken ctkn)
            {
                //communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.request, 1, 0, ctkn);
                Result<ICommunityMemberPagedResponse> response = await communitiesDataProvider.GetCommunityInviteRequestAsync(communityData?.id, InviteRequestAction.request, 1, 0, ctkn)
                                                                                              .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!response.Success)
                    return;

                RequestsAmount = response.Value.total;
            }
        }

        private void OnMainButtonClicked(ICommunityMemberData profile) =>
            OpenProfilePassport(profile);

        private void OnFriendButtonClicked(ICommunityMemberData profile) =>
            HandleContextMenuUserProfileButtonAsync(profile.ToUserData(), profile.FriendshipStatus.Convert());

        private void OnUnbanButtonClicked(ICommunityMemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            UnbanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid UnbanUserAsync(CancellationToken ct)
            {

                Result<bool> result = await communitiesDataProvider.UnBanUserFromCommunityAsync(profile.Address, communityData?.id, ct)
                                                           .SuppressToResultAsync(ReportCategory.COMMUNITIES);

                if (ct.IsCancellationRequested)
                    return;

                if (!result.Success || !result.Value)
                {
                    await inWorldWarningNotificationView.AnimatedShowAsync(UNBAN_USER_ERROR_TEXT, WARNING_NOTIFICATION_DURATION_MS, ct)
                                                        .SuppressToResultAsync(ReportCategory.COMMUNITIES);
                    return;
                }

                sectionsFetchData[MembersListView.MemberListSections.BANNED].Items.Remove(profile);
                RefreshGrid(false);
            }
        }
    }
}
