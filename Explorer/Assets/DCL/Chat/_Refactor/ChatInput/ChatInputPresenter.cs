using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Chat.Services;
using Utilities;
using Utility;

public class ChatInputPresenter : IDisposable
{
    private readonly ChatInputView view;
    private readonly IEventBus eventBus;
    private readonly ICurrentChannelService currentChannelService;
    private readonly GetUserChatStatusCommand getUserChatStatusCommand;
    private readonly SendMessageCommand sendMessageCommand;
    private readonly EventSubscriptionScope scope = new();

    public event Action<string>? OnMessageSubmitted;
    public event Action? OnFocusRequested;

    private CancellationTokenSource cts = new ();

    public ChatInputPresenter(
        ChatInputView view,
        IEventBus eventBus,
        ICurrentChannelService currentChannelService,
        GetUserChatStatusCommand getUserChatStatusCommand,
        SendMessageCommand sendMessageCommand)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.currentChannelService = currentChannelService;
        this.getUserChatStatusCommand = getUserChatStatusCommand;
        this.sendMessageCommand = sendMessageCommand;
        
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
        sendMessageCommand.Execute(new SendMessageCommandPayload { Body = message });
        view.SetText("");
    }

    private void HandleFocusRequested()
    {
        eventBus.Publish(new ChatEvents.FocusRequestedEvent());
    }
    
    public async UniTaskVoid OnFocus()
    {
        cts = cts.SafeRestart();
        
        bool canType = await CanTypeInChannelAsync(currentChannelService.CurrentChannel, cts.Token);
        if (cts.IsCancellationRequested) return;
        
        if (canType)
            view.SetActiveTyping();
    }
    
    public void OnDefocus()
    {
        cts.Cancel(); 
        view.SetDefault();
    }

    public void OnMinimize()
    {
        cts.Cancel();
        view.SetDefault();
    }
    
    public async UniTask UpdateStateForChannel(ChatChannel channel)
    {
        cts = cts.SafeRestart();

        bool canType = await CanTypeInChannelAsync(channel, cts.Token);
        if (cts.IsCancellationRequested) return;

        // If the user can type, we ensure the view is in its default, unblocked state.
        // If they CANNOT type, we do nothing, because CanTypeInChannelAsync has already
        // set the view to the correct blocked state. Overwriting it would be a bug.
        if (canType)
            view.SetDefault();
    }

    private async UniTask<bool> CanTypeInChannelAsync(ChatChannel channel, CancellationToken ct)
    {
        if (channel == null)
        {
            view.SetBlocked("No channel selected.");
            return false;
        }

        switch (channel.ChannelType)
        {
            case ChatChannel.ChatChannelType.NEARBY:
                return true;
            case ChatChannel.ChatChannelType.USER:
                {
                    view.SetBlocked("Checking user status...");
                    var status = await getUserChatStatusCommand.ExecuteAsync(channel.Id.Id, ct);
                    if (ct.IsCancellationRequested) return false;

                    switch (status)
                    {
                        case ChatUserStateUpdater.ChatUserState.CONNECTED:
                            return true; // Yes, we can type!

                        case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                            view.SetBlocked("To message this user you must first unblock them.");
                            return false;

                        case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                            view.SetBlocked("Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.");
                            return false;

                        case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                            view.SetBlocked("The user you are trying to message only accepts DMs from friends.");
                            return false;

                        case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
                        default:
                            view.SetBlocked("The user you are trying to message is offline.");
                            return false;
                    }

                    break;
                }
            default:
                view.SetBlocked("This chat type is not supported yet.");
                return false;
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