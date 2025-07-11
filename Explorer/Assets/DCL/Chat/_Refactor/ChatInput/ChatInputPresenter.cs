using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using Utility;

public class ChatInputPresenter : IDisposable
{
    private readonly IChatInputView view;
    private readonly ChatUserStateUpdater userStateUpdater;
    private readonly IChatEventBus chatEventBus;

    public event Action<string>? OnMessageSubmitted;
    public event Action? OnFocusRequested;

    private CancellationTokenSource cts = new ();

    public ChatInputPresenter(
        IChatInputView view,
        IChatEventBus chatEventBus,
        ChatUserStateUpdater userStateUpdater)
    {
        this.view = view;
        this.chatEventBus = chatEventBus;
        this.userStateUpdater = userStateUpdater;
    }

    public void Activate()
    {
        view.OnMessageSubmit += HandleMessageSubmitted;
        view.OnFocusRequested += HandleFocusRequested;
        view.OnInputChanged += OnInputChanged;
        chatEventBus.InsertTextInChat += HandleExternalTextInsert;
    }

    private void OnInputChanged(string input)
    {
    }

    private void HandleMessageSubmitted(string message)
    {
        OnMessageSubmitted?.Invoke(message);
        view.SetText("");
    }

    private void HandleFocusRequested()
    {
        OnFocusRequested?.Invoke();
    }

    public void Dispose()
    {
        view.OnMessageSubmit -= HandleMessageSubmitted;
        view.OnFocusRequested -= HandleFocusRequested;
        view.OnInputChanged -= OnInputChanged;
        chatEventBus.InsertTextInChat -= HandleExternalTextInsert;
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
        // 1. Cancel any previous async user state checks. This is always safe to do.
        cts = cts.SafeRestart();

        // 2. Handle the special case for the Nearby channel.
        if (channel.ChannelType == ChatChannel.ChatChannelType.NEARBY)
        {
            // The Nearby channel is always available.
            // Set the view to be interactable and we're done.
            view.SetInteractable(true);
            return;
        }

        // 3. If it's not the Nearby channel, it must be a user channel (DM).
        // Proceed with the asynchronous user state check as before.
        if (channel.ChannelType == ChatChannel.ChatChannelType.USER)
        {
            UpdateInputStateForUserAsync(channel.Id.Id, cts.Token).Forget();
        }
        else
        {
            // Handle other channel types like Community if they exist,
            // or disable for unknown types.
            view.SetInteractable(false, "This chat type is not supported yet.");
        }
    }

    private async UniTaskVoid UpdateInputStateForUserAsync(string userId, CancellationToken ct)
    {
        view.SetInteractable(false, "Checking user status...");

        // Fetch the state from the service
        var result = await userStateUpdater.GetChatUserStateAsync(userId, ct)
            .SuppressCancellationThrow()
            .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

        // If the operation was cancelled (e.g., user changed channels), we don't need to do anything.
        // The new operation will take over.
        if (ct.IsCancellationRequested) return;

        // Handle the case where the async operation failed
        if (!result.Success)
        {
            view.SetInteractable(false, "Could not retrieve user status.");
            return;
        }

        // Translate the model state from the service into a single, clear view command.
        switch (result.Value.Result)
        {
            case ChatUserStateUpdater.ChatUserState.CONNECTED:
                // Enable the input. The second parameter is not needed.
                view.SetInteractable(true);
                break;

            case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                // Disable the input and provide the specific reason.
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

    private void HandleExternalTextInsert(string text)
    {
        // The presenter is responsible for checking its own view's state
        // if (view.CurrentState == InputState.Enabled)
        // {
        //     view.InsertText(text);
        //     view.Focus();
        // }
    }
    
    public void Show()
    {
        view.Show();
    }
    
    public void Hide()
    {
        view.Hide();
    }
}