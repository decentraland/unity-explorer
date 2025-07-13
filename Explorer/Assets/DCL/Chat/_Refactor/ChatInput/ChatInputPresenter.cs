using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using Utility;

public class ChatInputPresenter : IDisposable
{
    private readonly IChatInputView view;
    private readonly IEventBus eventBus;
    private readonly GetUserChatStatusUseCase getUserChatStatusUseCase;
    private readonly SendMessageUseCase sendMessageUseCase;
    private readonly EventSubscriptionScope scope = new();

    public event Action<string>? OnMessageSubmitted;
    public event Action? OnFocusRequested;

    private CancellationTokenSource cts = new ();

    public ChatInputPresenter(
        IChatInputView view,
        IEventBus eventBus,
        GetUserChatStatusUseCase getUserChatStatusUseCase,
        SendMessageUseCase sendMessageUseCase)
    {
        this.view = view;
        this.eventBus = eventBus;
        this.getUserChatStatusUseCase = getUserChatStatusUseCase;
        this.sendMessageUseCase = sendMessageUseCase;
        
        view.OnMessageSubmit += HandleMessageSubmitted;
        view.OnFocusRequested += HandleFocusRequested;

        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
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

    private void HandleMessageSubmitted(string message)
    {
        sendMessageUseCase.Execute(new SendMessageCommand { Body = message });
        view.SetText("");
    }

    private void HandleFocusRequested()
    {
        eventBus.Publish(new ChatEvents.FocusRequestedEvent());
    }
    
    private void OnInputChanged(string input)
    {
        
    }

    public void SetActiveMode()
    {
        view.SetMode(IChatInputView.Mode.Active);
        view.Focus();
    }
    
    public void SetInactiveMode()
    {
        //view.SetMode(IChatInputView.Mode.InactiveAsButton, "Type a message...");
        view.Blur();
    }
    
    public void UpdateStateForChannel(ChatChannel channel)
    {
        cts = cts.SafeRestart();
        if (channel.ChannelType == ChatChannel.ChatChannelType.NEARBY)
        {
            view.SetInteractable(true);
            return;
        }

        if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
        {
            UpdateInputStateForUserAsync(channel.Id.Id, cts.Token).Forget();
        }
        else
        {
            view.SetInteractable(false, "This chat type is not supported yet.");
        }
    }

    private async UniTaskVoid UpdateInputStateForUserAsync(string userId, CancellationToken ct)
    {
        view.SetInteractable(false, "Checking user status...");
        
        var status = await getUserChatStatusUseCase.ExecuteAsync(userId, ct);
        if (ct.IsCancellationRequested) return;

        switch (status)
        {
            case ChatUserStateUpdater.ChatUserState.CONNECTED:
                view.SetInteractable(true);
                break;

            case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                view.SetInteractable(false, "To message this user you must first unblock them.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                view.SetInteractable(false, "Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                view.SetInteractable(false, "The user you are trying to message only accepts DMs from friends.");
                break;

            case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
            default:
                view.SetInteractable(false, "The user you are trying to message is offline.");
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