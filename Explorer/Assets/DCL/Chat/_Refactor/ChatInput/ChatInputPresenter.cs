using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using Utility;

public class ChatInputPresenter : IDisposable
{
    private readonly IChatInputView view;
    private readonly IEventBus eventBus;
    private readonly ICurrentChannelService currentChannelService;
    private readonly GetUserChatStatusUseCase getUserChatStatusUseCase;
    private readonly SendMessageUseCase sendMessageUseCase;
    private readonly EventSubscriptionScope scope = new();

    public event Action<string>? OnMessageSubmitted;
    public event Action? OnFocusRequested;

    private CancellationTokenSource cts = new ();

    public ChatInputPresenter(
        IChatInputView view,
        IEventBus eventBus,
        ICurrentChannelService currentChannelService,
        GetUserChatStatusUseCase getUserChatStatusUseCase,
        SendMessageUseCase sendMessageUseCase)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.currentChannelService = currentChannelService;
        this.getUserChatStatusUseCase = getUserChatStatusUseCase;
        this.sendMessageUseCase = sendMessageUseCase;
        
        view.OnMessageSubmit += HandleMessageSubmitted;
        view.OnFocusRequested += HandleFocusRequested;

        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
        scope.Add(eventBus.Subscribe<ChatEvents.CurrentChannelStateUpdatedEvent>(OnForceRefreshInputState));
    }

    public void Show()
    {
        view.Show();
    }
    
    public void Hide()
    {
        view.Hide();
    }
    
    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        UpdateStateForChannel(evt.Channel);
    }
    
    private void OnForceRefreshInputState(ChatEvents.CurrentChannelStateUpdatedEvent evt)
    {
        if (currentChannelService.CurrentChannel != null)
        {
            UpdateStateForChannel(currentChannelService.CurrentChannel);
        }
    }

    private void HandleMessageSubmitted(string message)
    {
        sendMessageUseCase.Execute(new SendMessageCommand { Body = message });
        view.SetText("");
    }

    private void HandleFocusRequested()
    {
        eventBus.Publish(new ChatEvents.FocusRequestedEvent());
    }
    
    public void OnFocus()
    {
        view.SetActiveTyping();
        UpdateCurrentChannelStatus();
    }
    
    public void OnDefocus()
    {
        view.SetDefault();
    }
    
    private void OnInputChanged(string input)
    {
        
    }

    public void OnMinimize()
    {
        view.SetDefault();
    }
    
    private void UpdateCurrentChannelStatus()
    {
        if (currentChannelService.CurrentChannel != null)
        {
            UpdateStateForChannel(currentChannelService.CurrentChannel);
        }
        else
        {
            // If there is no channel selected for some reason, block the input.
            view.SetBlocked("No channel selected.");
        }
    }
    
    public void UpdateStateForChannel(ChatChannel channel)
    {
        cts = cts.SafeRestart();
        if (channel.ChannelType == ChatChannel.ChatChannelType.NEARBY)
        {
            view.SetDefault();
            return;
        }

        if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
        {
            UpdateInputStateForUserAsync(channel.Id.Id, cts.Token).Forget();
        }
        else
        {
            view.SetBlocked("This chat type is not supported yet.");
        }
    }

    private async UniTaskVoid UpdateInputStateForUserAsync(string userId, CancellationToken ct)
    {
        view.SetBlocked("Checking user status...");
        
        var status = await getUserChatStatusUseCase.ExecuteAsync(userId, ct);
        if (ct.IsCancellationRequested) return;

        switch (status)
        {
            case ChatUserStateUpdater.ChatUserState.CONNECTED:
                view.SetDefault();
                break;

            case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                view.SetBlocked("To message this user you must first unblock them.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                view.SetBlocked("Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                view.SetBlocked("The user you are trying to message only accepts DMs from friends.");
                break;

            case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
            default:
                view.SetBlocked("The user you are trying to message is offline.");
                break;
        }
    }
    
    public void Dispose()
    {
        view.OnMessageSubmit -= HandleMessageSubmitted;
        view.OnFocusRequested -= HandleFocusRequested;
        scope.Dispose();
        cts.Cancel();
        cts.Dispose();
    }
}