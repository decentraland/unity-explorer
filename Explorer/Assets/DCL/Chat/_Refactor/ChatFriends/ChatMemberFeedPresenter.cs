using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatServices.ChatContextService;
using DCL.Chat.ChatViewModels;
using DCL.Chat.ChatViews;
using DCL.Chat.History;
using DCL.Optimization.Pools;
using DCL.UI.UpgradeGuestAccountPopup;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatFriends
{
    public class ChatMemberFeedPresenter : IDisposable
    {
        private readonly ChannelMemberFeedView view;
        private readonly IEventBus eventBus;
        private readonly ChatEventBus chatEventBus;
        private readonly GetChannelMembersCommand getChannelMembersCommand;
        private readonly ChatMemberListService memberListService;
        private readonly ChatContextMenuService chatContextMenuService;
        private readonly IMVCManager mvcManager;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IChatHistory chatHistory;

        private readonly EventSubscriptionScope scope = new ();
        private CancellationTokenSource lifeCts = new ();

        private readonly List<ChatMemberListViewModel> currentMembers = new (PoolConstants.AVATARS_COUNT);

        public ChatMemberFeedPresenter(
            ChannelMemberFeedView view,
            IEventBus eventBus,
            ChatEventBus chatEventBus,
            ChatMemberListService memberListService,
            ChatContextMenuService chatContextMenuService,
            GetChannelMembersCommand getChannelMembersCommand,
            IMVCManager mvcManager,
            IWeb3IdentityCache identityCache,
            IChatHistory chatHistory)
        {
            this.view = view;
            this.eventBus = eventBus;
            this.chatEventBus = chatEventBus;
            this.memberListService = memberListService;
            this.getChannelMembersCommand = getChannelMembersCommand;
            this.chatContextMenuService = chatContextMenuService;
            this.mvcManager = mvcManager;
            this.identityCache = identityCache;
            this.chatHistory = chatHistory;

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

        private void HandleLiveUpdate(IReadOnlyList<ChatMemberListData> freshMembers)
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

            // A guest can answer a conversation someone else started, but cannot start one
            if (identityCache.IsGuest() && !chatHistory.Channels.ContainsKey(new ChatChannel.ChannelId(userId)))
            {
                mvcManager.ShowAndForget(UpgradeGuestAccountPopupController.IssueCommand(new UpgradeGuestAccountPopupController.Params(GuestUpgradeTrigger.DirectMessage)));
                return;
            }

            chatEventBus.RaiseOpenPrivateConversationRequestedEvent(userId);
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
