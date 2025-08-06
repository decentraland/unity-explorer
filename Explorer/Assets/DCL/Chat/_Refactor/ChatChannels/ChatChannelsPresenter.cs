using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.ChatViewModels;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.UI.Profiles.Helpers;
using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Utility;

namespace DCL.Chat
{
    public class ChatChannelsPresenter : IDisposable
    {
        private readonly ChatChannelsView view;
        private readonly IEventBus eventBus;
        private readonly IChatEventBus chatEventBus;
        private readonly IChatHistory chatHistory;
        private readonly CurrentChannelService currentChannelService;
        private readonly SelectChannelCommand selectChannelCommand;
        private readonly CloseChannelCommand closeChannelCommand;
        private readonly OpenConversationCommand openConversationCommand;
        private readonly CreateChannelViewModelCommand createChannelViewModelCommand;
        private readonly Dictionary<ChatChannel.ChannelId, BaseChannelViewModel> viewModels = new ();

        private bool isInitialized;

        private CancellationTokenSource lifeCts;
        private readonly EventSubscriptionScope scope = new ();

        public ChatChannelsPresenter(ChatChannelsView view,
            IEventBus eventBus,
            IChatEventBus chatEventBus,
            IChatHistory chatHistory,
            CurrentChannelService currentChannelService,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            SelectChannelCommand selectChannelCommand,
            CloseChannelCommand closeChannelCommand,
            OpenConversationCommand openConversationCommand,
            CreateChannelViewModelCommand createChannelViewModelCommand)
        {
            this.view = view;
            this.view.Initialize(profileRepositoryWrapper);

            this.eventBus = eventBus;
            this.chatEventBus = chatEventBus;
            this.chatHistory = chatHistory;
            this.currentChannelService = currentChannelService;
            this.selectChannelCommand = selectChannelCommand;
            this.closeChannelCommand = closeChannelCommand;
            this.openConversationCommand = openConversationCommand;
            this.createChannelViewModelCommand = createChannelViewModelCommand;

            lifeCts = new CancellationTokenSource();

            view.ConversationSelected += OnViewConversationSelected;
            view.ConversationRemovalRequested += OnViewConversationRemovalRequested;

            this.chatHistory.ChannelAdded += OnRuntimeChannelAdded;
            this.chatHistory.ChannelRemoved += OnChannelRemoved;
            this.chatHistory.ReadMessagesChanged += OnReadMessagesChanged;
            this.chatHistory.MessageAdded += OnMessageAdded;
            this.chatEventBus.OpenPrivateConversationRequested += OnOpenUserConversation;
            this.chatEventBus.OpenCommunityConversationRequested += OnOpenCommunityConversation;

            scope.Add(this.eventBus.Subscribe<ChatEvents.UserStatusUpdatedEvent>(OnLiveUserConnectionStateChange));
            scope.Add(this.eventBus.Subscribe<ChatEvents.InitialChannelsLoadedEvent>(OnInitialChannelsLoaded));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUpdatedEvent>(OnChannelUpdated));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelAddedEvent>(OnChannelAdded));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelLeftEvent>(OnChannelLeft));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnSystemChannelSelected));
            scope.Add(this.eventBus.Subscribe<ChatEvents.ChannelUsersStatusUpdated>(OnChannelUsersStatusUpdated));
        }

        private void OnLiveUserConnectionStateChange(ChatEvents.UserStatusUpdatedEvent userStatusUpdatedEvent)
        {
            if (userStatusUpdatedEvent.ChannelType != ChatChannel.ChatChannelType.USER) return;

            var channelId = new ChatChannel.ChannelId(userStatusUpdatedEvent.UserId);

            if (viewModels.TryGetValue(channelId, out BaseChannelViewModel? baseVm) &&
                baseVm is UserChannelViewModel userVm)
            {
                userVm.IsOnline = userStatusUpdatedEvent.IsOnline;
                view.UpdateConversation(userVm);
            }
        }

        private void OnChannelUsersStatusUpdated(ChatEvents.ChannelUsersStatusUpdated @event)
        {
            if (@event.ChannelType != ChatChannel.ChatChannelType.USER) return;

            foreach (KeyValuePair<ChatChannel.ChannelId, BaseChannelViewModel> kvp in viewModels)
            {
                if (kvp.Value is not UserChannelViewModel userVm) continue;

                bool isOnline = @event.OnlineUsers.Contains(kvp.Key.Id);
                userVm.IsOnline = isOnline;
                view.UpdateConversation(userVm);
            }
        }

        private void OnOpenUserConversation(string userId)
        {
            openConversationCommand.Execute(userId, ChatChannel.ChatChannelType.USER, lifeCts.Token);
        }

        private void OnOpenCommunityConversation(string userId)
        {
            openConversationCommand.Execute(userId, ChatChannel.ChatChannelType.COMMUNITY, lifeCts.Token);
        }

        private void OnChannelUpdated(ChatEvents.ChannelUpdatedEvent evt)
        {
            if (viewModels.TryGetValue(evt.ViewModel.Id, out _))
            {
                viewModels[evt.ViewModel.Id] = evt.ViewModel;
                view.UpdateConversation(evt.ViewModel);
            }
        }

        private void OnInitialChannelsLoaded(ChatEvents.InitialChannelsLoadedEvent evt)
        {
            if (isInitialized) return;

            lifeCts.Cancel();
            lifeCts.Dispose();
            lifeCts = new CancellationTokenSource();

            view.Clear();
            viewModels.Clear();

            foreach (ChatChannel? channel in evt.Channels) { AddChannelToView(channel); }

            isInitialized = true;
        }

        private void OnRuntimeChannelAdded(ChatChannel channel)
        {
            if (!isInitialized) return;
            AddChannelToView(channel);
        }

        private void OnChannelRemoved(ChatChannel.ChannelId removedChannel, ChatChannel.ChatChannelType channelType)
        {
            if (!isInitialized) return;
            viewModels.Remove(removedChannel);
            view.RemoveConversation(removedChannel);

            if (currentChannelService.CurrentChannelId.Equals(removedChannel))
                selectChannelCommand.Execute(ChatChannel.NEARBY_CHANNEL_ID, lifeCts.Token);
        }

        private void OnChannelAdded(ChatEvents.ChannelAddedEvent evt)
        {
            AddChannelToView(evt.Channel);
        }

        private void OnSystemChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            view.SelectConversation(evt.Channel.Id);
        }

        private void OnViewConversationSelected(ChatChannel.ChannelId channelId)
        {
            selectChannelCommand.Execute(channelId, lifeCts.Token);
        }

        private void OnViewConversationRemovalRequested(ChatChannel.ChannelId channelId)
        {
            closeChannelCommand.Execute(channelId);
        }

        private void OnChannelLeft(ChatEvents.ChannelLeftEvent evt)
        {
            viewModels.Remove(evt.Channel.Id);
            view.RemoveConversation(evt.Channel);
        }

        private void AddChannelToView(ChatChannel channel)
        {
            BaseChannelViewModel viewModel = createChannelViewModelCommand
               .CreateViewModelAndFetch(channel, lifeCts.Token);

            viewModels[viewModel.Id] = viewModel;
            view.AddConversation(viewModel);

            if (isInitialized)
                view.MoveChannelToTop(channel.Id);
        }

        private void OnMessageAdded(ChatChannel destinationChannel, ChatMessage addedMessage, int _)
        {
            if (addedMessage is { IsSentByOwnUser: true, IsSystemMessage: false }) return;

            if (chatHistory.Channels.TryGetValue(destinationChannel.Id, out var channel))
            {
                UpdateUnreadStatus(channel);
            }

            if (destinationChannel.ChannelType != ChatChannel.ChatChannelType.NEARBY)
            {
                view.MoveChannelToTop(destinationChannel.Id);
            }
        }

        private void OnReadMessagesChanged(ChatChannel changedChannel)
        {
            UpdateUnreadStatus(changedChannel);
        }

        private void UpdateUnreadStatus(ChatChannel channel)
        {
            (int unreadCount, bool hasMentions) = CalculateUnreadStatus(channel);

            if (viewModels.TryGetValue(channel.Id, out var vm))
            {
                vm.UnreadMessagesCount = unreadCount;
                vm.HasUnreadMentions = hasMentions;
            }

            view.SetUnreadMessages(channel.Id, unreadCount, hasMentions);
        }

        public void Show()
        {
            view.Show();
        }

        public void Hide()
        {
            view.Hide();
        }

        public void SetFocusState(bool isFocused, bool animate, float duration, Ease easing)
        {
            view.SetFocusedState(isFocused, animate, duration, easing);
        }

        private (int unreadCount, bool hasMentions) CalculateUnreadStatus(ChatChannel channel)
        {
            if (channel == null)
                return (0, false);

            int unreadCount = channel.Messages.Count - channel.ReadMessages;

            bool hasMentions = false;

            if (unreadCount > 0)
            {
                var messages = channel.Messages;

                for (int i = 0; i < unreadCount; i++)
                {
                    if (messages[i].IsMention)
                    {
                        hasMentions = true;
                        break;
                    }
                }
            }

            return (unreadCount, hasMentions);
        }

        public void Dispose()
        {
            lifeCts.SafeCancelAndDispose();

            view.ConversationSelected -= OnViewConversationSelected;
            view.ConversationRemovalRequested -= OnViewConversationRemovalRequested;

            chatHistory.ChannelAdded -= OnRuntimeChannelAdded;
            chatHistory.ChannelRemoved -= OnChannelRemoved;
            chatHistory.MessageAdded -= OnMessageAdded;
            chatHistory.ReadMessagesChanged -= OnReadMessagesChanged;

            chatEventBus.OpenPrivateConversationRequested -= OnOpenUserConversation;
            chatEventBus.OpenCommunityConversationRequested -= OnOpenCommunityConversation;

            scope.Dispose();
        }
    }
}
