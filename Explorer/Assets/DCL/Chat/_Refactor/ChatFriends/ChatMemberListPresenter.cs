using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.Chat.ChatViews;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Chat.EventBus;
using Utility;

namespace DCL.Chat.ChatFriends
{
    public class ChatMemberListPresenter : IDisposable
    {
        private readonly ChannelMemberFeedView view;
        private readonly IEventBus eventBus;
        private readonly IChatEventBus chatEventBus;
        private readonly GetChannelMembersCommand getChannelMembersCommand;
        private readonly ChatMemberListService memberListService;
        private readonly ChatContextMenuService chatContextMenuService;

        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource lifeCts = new ();

        private readonly List<ChatMemberListViewModel> currentMembers = new (PoolConstants.AVATARS_COUNT);

        public ChatMemberListPresenter(
            ChannelMemberFeedView view,
            IEventBus eventBus,
            IChatEventBus chatEventBus,
            ChatMemberListService memberListService,
            ChatContextMenuService chatContextMenuService,
            GetChannelMembersCommand getChannelMembersCommand)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.chatEventBus = chatEventBus;
            this.memberListService = memberListService;
            this.getChannelMembersCommand = getChannelMembersCommand;
            this.chatContextMenuService = chatContextMenuService;

            this.view.OnMemberContextMenuRequested += OnMemberContextMenuRequested;
            this.view.OnMemberItemRequested += OnMemberSelectionRequested;
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChatResetEvent>(OnChatResetEvent));
        }

        private void ShowAndLoad()
        {
            view.Show();

            lifeCts = new CancellationTokenSource();
            memberListService.StartLiveMemberUpdates(HandleLiveUpdate);
            memberListService.RequestInitialMemberListAsync().Forget();
        }

        public void Hide()
        {
            currentMembers.Clear();
            view.SetData(currentMembers);

            view.Hide();
            memberListService.StopLiveMemberUpdates();

            lifeCts.SafeCancelAndDispose();
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

        private void OnMemberContextMenuRequested(UserProfileMenuRequest data)
        {
            chatContextMenuService
               .ShowUserProfileMenuAsync(data)
               .Forget();
        }
        
        private void OnMemberSelectionRequested(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return;
            
            chatEventBus.OpenPrivateConversationUsingUserId(userId);
        }

        private void OnChatResetEvent(ChatEvents.ChatResetEvent evt)
        {
            Hide();
        }
        
        public void Dispose()
        {
            lifeCts.SafeCancelAndDispose();
            scope.Dispose();
        }
    }
}
