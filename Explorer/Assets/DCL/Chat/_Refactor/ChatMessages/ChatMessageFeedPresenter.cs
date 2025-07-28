using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat.History;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.MessageBus;
using DCL.Chat.Services;
using DCL.Diagnostics;
using DCL.UI;
using DCL.UI.Profiles.Helpers;
using DCL.Web3;
using DG.Tweening;
using MVC;
using UnityEngine;
using Utility;

public class ChatMessageFeedPresenter : IDisposable
{
    private readonly ChatMessageFeedView view;
    private readonly IEventBus eventBus;
    private readonly IChatMessagesBus chatMessageBus;
    private readonly ICurrentChannelService currentChannelService;
    private readonly ChatContextMenuService contextMenuService;
    private readonly GetMessageHistoryCommand getMessageHistoryCommand;
    private readonly LoadAndDisplayMessagesCommand loadAndDisplayMessagesCommand;
    private readonly CreateMessageViewModelCommand createMessageViewModelCommand;
    private readonly ProcessAndAddMessageCommand processAndAddMessageCommand;
    private readonly MarkChannelAsReadCommand markChannelAsReadCommand;

    private readonly EventSubscriptionScope scope = new();
    private CancellationTokenSource loadChannelCts = new();

    public ChatMessageFeedPresenter(ChatMessageFeedView view,
        IEventBus eventBus,
        IChatMessagesBus chatMessageBus,
        ICurrentChannelService currentChannelService,
        ChatContextMenuService contextMenuService,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        GetMessageHistoryCommand getMessageHistoryCommand,
        CreateMessageViewModelCommand createMessageViewModelCommand,
        LoadAndDisplayMessagesCommand loadAndDisplayMessagesCommand,
        ProcessAndAddMessageCommand processAndAddMessageCommand,
        MarkChannelAsReadCommand markChannelAsReadCommand)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.chatMessageBus = chatMessageBus;
        this.currentChannelService = currentChannelService;
        this.contextMenuService = contextMenuService;
        this.getMessageHistoryCommand = getMessageHistoryCommand;
        this.createMessageViewModelCommand = createMessageViewModelCommand;
        this.loadAndDisplayMessagesCommand = loadAndDisplayMessagesCommand;
        this.processAndAddMessageCommand = processAndAddMessageCommand;
        this.markChannelAsReadCommand = markChannelAsReadCommand;

        view.SetExternalDependencies(profileRepositoryWrapper, createMessageViewModelCommand);
        view.Initialize();

        view.OnFakeMessageRequested += OnFakeMessageRequested;
        view.OnChatContextMenuRequested += OnChatContextMenuRequested;
        view.OnProfileContextMenuRequested += OnProfileContextMenuRequested;
        view.OnScrollToBottom += MarkCurrentChannelAsRead;
        
        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
        scope.Add(eventBus.Subscribe<ChatEvents.MessageReceivedEvent>(OnMessageReceived));
        scope.Add(eventBus.Subscribe<ChatEvents.ChatHistoryClearedEvent>(OnChatHistoryCleared));
        scope.Add(eventBus.Subscribe<ChatEvents.ChatMessageUpdatedEvent>(OnMessageUpdated));

        chatMessageBus.MessageAdded += OnMessageAdded;
    }

    private void OnMessageUpdated(ChatEvents.ChatMessageUpdatedEvent evt)
    {
        //view.UpdateMessage(evt.ViewModel);
    }

    private void OnMessageAdded(ChatChannel.ChannelId channelId,
        ChatChannel.ChatChannelType channelType, ChatMessage message)
    {
        if (!currentChannelService.CurrentChannelId.Equals(channelId))
            return;

        // var viewModel = processAndAddMessageCommand.Execute(channelId, currentChannelService.CurrentChannelType, message, loadChannelCts.Token);
        // ReportHub.Log(ReportData.UNSPECIFIED, $"OnMessageAdded: {viewModel.Message} in channel {channelId.Id}");
        // view.AppendMessage(viewModel);
        view.AppendMessage(message, true);
    }

    private void OnProfileContextMenuRequested(string userId, Vector2 position)
    {
        var request = new UserProfileMenuRequest
        {
            WalletAddress = new Web3Address(userId), Position = position, AnchorPoint = MenuAnchorPoint.TOP_RIGHT, Offset = Vector2.zero
        };

        contextMenuService.ShowUserProfileMenuAsync(request).Forget();
    }

    private void OnChatContextMenuRequested(string message, ChatEntryView? chatEntry)
    {
        var request = new ChatEntryMenuPopupData(chatEntry.messageBubbleElement.PopupPosition,
            message, chatEntry.messageBubbleElement.HideOptionsButton);

        contextMenuService.ShowChatOptionsAsync(request).Forget();
    }

    private void OnFakeMessageRequested()
    {
        var currentChannelId = currentChannelService.CurrentChannelId;
        eventBus.Publish(new ChatEvents.MessageReceivedEvent
        {
            Message = new ChatMessage("some message", "validated name",
                currentChannelId.Id,
                true, "sds"),
            ChannelId = currentChannelService.CurrentChannelId
        });
    }

    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        loadChannelCts = loadChannelCts.SafeRestart();
        LoadChannelAsync(evt.Channel.Id, loadChannelCts.Token).Forget();
    }

    public void Activate()
    {
        view.OnScrollToBottom += MarkCurrentChannelAsRead;
    }

    public void Deactivate()
    {
        view.OnScrollToBottom -= MarkCurrentChannelAsRead;
    }

    private async UniTask LoadChannelAsync(ChatChannel.ChannelId channelId, CancellationToken token)
    {
        var messages = await getMessageHistoryCommand
            .GetChatMessagesExecuteAsync(channelId, token);
        
        if (token.IsCancellationRequested) return;

        view.SetData(messages);
        view.ShowLastMessage();

        await UniTask.Yield();
        if (token.IsCancellationRequested) return;
        MarkCurrentChannelAsRead();
    }

    private void OnMessageReceived(ChatEvents.MessageReceivedEvent evt)
    {
        if (!currentChannelService.CurrentChannelId.Equals(evt.ChannelId))
            return;

        AddNewMessageByRecreatingList(evt.Message);
        
        // var viewModel = createMessageViewModelCommand.Execute(evt.Message);
        // view.AppendMessage(viewModel, true);
        //
        // if (view.IsAtBottom())
        //     MarkCurrentChannelAsRead();
    }

    private void AddNewMessageByRecreatingList(ChatMessage newMessage)
    {
        // 1. Get the current list of messages from the view.
        var currentMessages = new List<ChatMessage>(view.GetCurrentMessages());

        // 2. Add the new message.
        currentMessages.Add(newMessage);

        // 3. Tell the view to render this new, complete list.
        view.SetData(currentMessages);

        // 4. Ensure we are scrolled to the bottom to see the new message.
        view.ShowLastMessage();
    }

    private void MarkCurrentChannelAsRead()
    {
        markChannelAsReadCommand.Execute(currentChannelService.CurrentChannelId);
    }

    private void OnChatHistoryCleared(ChatEvents.ChatHistoryClearedEvent evt)
    {
        if (currentChannelService.CurrentChannelId.Equals(evt.ChannelId))
            view.Clear();
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

    public void Dispose()
    {
        chatMessageBus.MessageAdded -= OnMessageAdded;
        
        loadChannelCts.SafeCancelAndDispose();
        Deactivate();
        scope.Dispose();
        if (view != null)
            view.OnScrollToBottom -= MarkCurrentChannelAsRead;
    }
}
