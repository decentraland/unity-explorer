using Cysharp.Threading.Tasks;
using DCL.Chat.ChatUseCases;
using DCL.Chat.ChatViewModels;
using DCL.Chat.Services;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatFriends
{
    public class ChatMemberListPresenter : IDisposable
    {
        private readonly ChannelMemberFeedView view;
        private readonly IEventBus eventBus;
        private readonly GetChannelMembersCommand getChannelMembersCommand;
        private readonly ChatMemberListService memberListService;
        private readonly ChatContextMenuService chatContextMenuService;

        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource lifeCts = new ();

        private readonly List<ChatMemberListViewModel> currentMembers = new (PoolConstants.AVATARS_COUNT);

        public ChatMemberListPresenter(
            ChannelMemberFeedView view,
            IEventBus eventBus,
            ChatMemberListService memberListService,
            ChatContextMenuService chatContextMenuService,
            GetChannelMembersCommand getChannelMembersCommand)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.memberListService = memberListService;
            this.getChannelMembersCommand = getChannelMembersCommand;
            this.chatContextMenuService = chatContextMenuService;

            this.view.OnMemberContextMenuRequested += OnMemberContextMenuRequested;
        }

        private void ShowAndLoad()
        {
            view.Show();

            lifeCts = new CancellationTokenSource();
            memberListService.StartLiveMemberUpdates();
            memberListService.OnMemberListUpdated += HandleLiveUpdate;
            memberListService.RequestInitialMemberListAsync().Forget();
        }

        private void HandleLiveUpdate(IReadOnlyList<ChatMemberListView.MemberData> freshMembers)
        {
            lifeCts = lifeCts.SafeRestart();

            // Get the list of initial view models from the command.
            // The command will handle starting the thumbnail downloads.
            getChannelMembersCommand.GetInitialMembersAndStartLoadingThumbnails(freshMembers,
                currentMembers, lifeCts.Token);

            // Immediately display this list. Names appear instantly, pictures are loading.
            view.SetData(currentMembers);
        }

        public void Show() =>
            ShowAndLoad();

        public void Hide()
        {
            currentMembers.Clear();
            view.SetData(currentMembers);

            view.Hide();
            memberListService.StopLiveMemberUpdates();
            memberListService.OnMemberListUpdated -= HandleLiveUpdate;

            lifeCts.SafeCancelAndDispose();
        }

        private void OnMemberContextMenuRequested(UserProfileMenuRequest data)
        {
            chatContextMenuService
               .ShowUserProfileMenuAsync(data)
               .Forget();
        }

        public void Dispose()
        {
            memberListService.OnMemberListUpdated -= HandleLiveUpdate;
            lifeCts.SafeCancelAndDispose();
            scope.Dispose();
        }
    }
}
