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
using DCL.Utilities.Extensions;
using DCL.Web3;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Types;

namespace DCL.Communities.CommunitiesCard.Members
{
    public class MembersListController : IDisposable
    {
        private static readonly RectOffset CONTEXT_MENU_VERTICAL_LAYOUT_PADDING = new (15, 15, 20, 25);
        private const int CONTEXT_MENU_SEPARATOR_HEIGHT = 20;
        private const int CONTEXT_MENU_ELEMENTS_SPACING = 5;
        private const int PAGE_SIZE = 20;

        private const string KICK_MEMBER_TEXT_FORMAT = "Are you sure you want to kick '{0}' from {1}?";
        private const string BAN_MEMBER_TEXT_FORMAT = "Are you sure you want to ban '{0}' from {1}?";

        private const string KICK_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string KICK_MEMBER_CONFIRM_TEXT = "KICK";
        private const string BAN_MEMBER_CANCEL_TEXT = "CANCEL";
        private const string BAN_MEMBER_CONFIRM_TEXT = "BAN";

        private readonly MembersListView view;
        private readonly ConfirmationDialogView confirmationDialogView;
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

        private readonly SectionFetchData allMembersFetchData = new (PAGE_SIZE);
        private readonly SectionFetchData bannedMembersFetchData = new (PAGE_SIZE);

        private GetCommunityResponse.CommunityData? communityData = null;
        private CancellationToken cancellationToken;
        private bool isFetching;
        private bool viewerCanEdit => communityData?.role is CommunityMemberRole.moderator or CommunityMemberRole.owner;
        private SectionFetchData currentSectionFetchData => currentSection == MembersListView.MemberListSections.ALL ? allMembersFetchData : bannedMembersFetchData;

        private GetCommunityMembersResponse.MemberData lastClickedProfileCtx;
        private CancellationTokenSource friendshipOperationCts = new ();
        private CancellationTokenSource contextMenuOperationCts = new ();
        private UniTaskCompletionSource? panelLifecycleTask;
        private MembersListView.MemberListSections currentSection = MembersListView.MemberListSections.ALL;

        public MembersListController(MembersListView view,
            ConfirmationDialogView confirmationDialogView,
            ViewDependencies viewDependencies,
            IMVCManager mvcManager,
            ObjectProxy<IFriendsService> friendServiceProxy,
            ICommunitiesDataProvider communitiesDataProvider)
        {
            this.view = view;
            this.confirmationDialogView = confirmationDialogView;
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

            this.view.InitGrid(() => currentSectionFetchData);
            this.view.ActiveSectionChanged += OnMemberListSectionChanged;
            this.view.NewDataRequested += OnNewDataRequested;
            this.view.ElementMainButtonClicked += MainButtonClicked;
            this.view.ElementContextMenuButtonClicked += ContextMenuButtonClicked;
            this.view.ElementFriendButtonClicked += FriendButtonClicked;
            this.view.ElementUnbanButtonClicked += UnbanButtonClicked;

            this.view.InjectDependencies(viewDependencies);
        }

        public void Dispose()
        {
            contextMenuOperationCts.SafeCancelAndDispose();
            friendshipOperationCts.SafeCancelAndDispose();
            view.ActiveSectionChanged -= OnMemberListSectionChanged;
            view.NewDataRequested -= OnNewDataRequested;
            view.ElementMainButtonClicked -= MainButtonClicked;
            view.ElementContextMenuButtonClicked -= ContextMenuButtonClicked;
            view.ElementFriendButtonClicked -= FriendButtonClicked;
            view.ElementUnbanButtonClicked -= UnbanButtonClicked;
        }

        private void OnNewDataRequested()
        {
            if (isFetching) return;

            FetchNewDataAsync(cancellationToken).Forget();
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

                SectionFetchData sectionData = currentSectionFetchData;

                if (sectionData.pageNumber == 0)
                    FetchNewDataAsync(cancellationToken).Forget();
                else
                    view.RefreshGrid();
            }
        }

        private void BlockUserClicked(GetCommunityMembersResponse.MemberData profile) =>
            mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(profile.id), profile.name, BlockUserPromptParams.UserBlockAction.BLOCK)), cancellationToken).Forget();

        private void BanUser(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            BanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid BanUserAsync(CancellationToken token)
            {
                try
                {
                    ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                        new ConfirmationDialogView.DialogData(string.Format(BAN_MEMBER_TEXT_FORMAT, profile.name, communityData?.name),
                            BAN_MEMBER_CANCEL_TEXT,
                            BAN_MEMBER_CONFIRM_TEXT,
                            view.BanSprite,
                            false, false),
                        cancellationToken);

                    if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                    bool result = await communitiesDataProvider.BanUserFromCommunityAsync(profile.id, communityData?.id, token);

                    if (result)
                    {
                        allMembersFetchData.members.Remove(profile);

                        List<GetCommunityMembersResponse.MemberData> memberList = bannedMembersFetchData.members;
                        profile.role = CommunityMemberRole.none;
                        memberList.Add(profile);

                        MembersSorter.SortMembersList(memberList);

                        view.RefreshGrid();
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
                    ConfirmationDialogView.ConfirmationResult dialogResult = await confirmationDialogView.ShowConfirmationDialogAsync(
                        new ConfirmationDialogView.DialogData(string.Format(KICK_MEMBER_TEXT_FORMAT, profile.name, communityData?.name),
                            KICK_MEMBER_CANCEL_TEXT,
                            KICK_MEMBER_CONFIRM_TEXT,
                            view.KickSprite,
                            false, false),
                        cancellationToken);

                    if (dialogResult == ConfirmationDialogView.ConfirmationResult.CANCEL) return;

                    bool result = await communitiesDataProvider.KickUserFromCommunityAsync(profile.id, communityData?.id, token);

                    if (result)
                    {
                        allMembersFetchData.members.Remove(profile);
                        view.RefreshGrid();
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
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            AddModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid AddModeratorAsync(CancellationToken token)
            {
                try
                {
                    bool result = await communitiesDataProvider.SetMemberRoleAsync(profile.id, communityData?.id, CommunityMemberRole.moderator, token);

                    if (result)
                    {
                        List<GetCommunityMembersResponse.MemberData> memberList = allMembersFetchData.members;

                        foreach (GetCommunityMembersResponse.MemberData member in memberList)
                            if (member.id.Equals(profile.id))
                            {
                                member.role = CommunityMemberRole.moderator;
                                break;
                            }

                        MembersSorter.SortMembersList(memberList);

                        view.RefreshGrid();
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
        }

        private void RemoveModerator(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            RemoveModeratorAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid RemoveModeratorAsync(CancellationToken token)
            {
                try
                {
                    bool result = await communitiesDataProvider.SetMemberRoleAsync(profile.id, communityData?.id, CommunityMemberRole.member, token);

                    if (result)
                    {
                        List<GetCommunityMembersResponse.MemberData> memberList = allMembersFetchData.members;
                        foreach (GetCommunityMembersResponse.MemberData member in memberList)
                            if (member.id.Equals(profile.id))
                            {
                                member.role = CommunityMemberRole.member;
                                break;
                            }

                        MembersSorter.SortMembersList(memberList);

                        view.RefreshGrid();
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    ReportHub.LogException(e, new ReportData(ReportCategory.COMMUNITIES));
                }
            }
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
            mvcManager.ShowAsync(PassportController.IssueCommand(new PassportController.Params(profile.id)), cancellationToken).Forget();

        public void Reset()
        {
            communityData = null;

            allMembersFetchData.Reset();
            bannedMembersFetchData.Reset();

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
                    mvcManager.ShowAsync(BlockUserPromptController.IssueCommand(new BlockUserPromptParams(new Web3Address(userData.userAddress), userData.userName, BlockUserPromptParams.UserBlockAction.UNBLOCK)), cancellationToken).Forget();
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

        private async UniTaskVoid FetchNewDataAsync(CancellationToken ct)
        {
            isFetching = true;

            SectionFetchData membersData = currentSectionFetchData;

            membersData.pageNumber++;
            await FetchDataAsync(ct);
            membersData.totalFetched = membersData.pageNumber * PAGE_SIZE;

            view.RefreshGrid();

            isFetching = false;
        }

        private async UniTask FetchDataAsync(CancellationToken ct)
        {
            SectionFetchData membersData = currentSectionFetchData;

            Result<GetCommunityMembersResponse> response = await communitiesDataProvider.GetCommunityMembersAsync(communityData?.id, currentSection == MembersListView.MemberListSections.BANNED, membersData.pageNumber, PAGE_SIZE, ct)
                                                                                            .SuppressToResultAsync(ReportCategory.COMMUNITIES);

            if (!response.Success)
            {
                //If the request fails, we restore the previous page number in order to retry the same request next time
                membersData.pageNumber--;
                return;
            }

            foreach (var member in response.Value.members)
                if (!membersData.members.Contains(member))
                    membersData.members.Add(member);

            MembersSorter.SortMembersList(membersData.members);

            membersData.totalToFetch = response.Value.totalAmount;
        }

        public void ShowMembersListAsync(GetCommunityResponse.CommunityData community, CancellationToken ct)
        {
            cancellationToken = ct;

            if (communityData is not null && community.id.Equals(communityData.Value.id)) return;

            communityData = community;
            view.SetSectionButtonsActive(communityData?.role is CommunityMemberRole.moderator or CommunityMemberRole.owner);
            panelLifecycleTask = new UniTaskCompletionSource();

            FetchNewDataAsync(ct).Forget();
        }

        private void MainButtonClicked(GetCommunityMembersResponse.MemberData profile)
        {
            // Handle main button click
            // Debug.Log("MainButtonClicked: " + profile.id);
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
                           closeTask: panelLifecycleTask?.Task)), cancellationToken)
                      .Forget();
        }

        private void FriendButtonClicked(GetCommunityMembersResponse.MemberData profile) =>
            HandleContextMenuUserProfileButton(profile.ToUserData(), ConvertFriendshipStatus(profile.friendshipStatus));

        private void UnbanButtonClicked(GetCommunityMembersResponse.MemberData profile)
        {
            contextMenuOperationCts = contextMenuOperationCts.SafeRestart();
            UnbanUserAsync(contextMenuOperationCts.Token).Forget();
            return;

            async UniTaskVoid UnbanUserAsync(CancellationToken ct)
            {
                try
                {
                    bool result = await communitiesDataProvider.UnBanUserFromCommunityAsync(profile.id, communityData?.id, ct);

                    if (result)
                    {
                        bannedMembersFetchData.members.Remove(profile);
                        view.RefreshGrid();
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
