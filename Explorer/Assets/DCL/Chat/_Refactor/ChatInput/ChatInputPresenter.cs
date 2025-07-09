using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.EventBus;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Settings.Settings;
using DCL.Utilities.Extensions;
using Utility;

public class ChatInputPresenter : IDisposable
{
    private readonly IChatInputView view;
    private readonly ChatSettingsAsset chatSettings;
    private readonly DCLInput dclInput;
    private readonly ChatUserStateUpdater userStateUpdater;
    private readonly IChatEventBus chatEventBus;

    public event Action<string>? OnMessageSubmitted;
    public event Action<bool>? OnFocusChanged;

    private CancellationTokenSource cts = new ();

    public ChatInputPresenter(
        IChatInputView view,
        ChatSettingsAsset chatSettings,
        IChatEventBus chatEventBus,
        ChatUserStateUpdater userStateUpdater)
    {
        this.view = view;
        this.chatSettings = chatSettings;
        this.chatEventBus = chatEventBus;
        this.userStateUpdater = userStateUpdater;
    }

    public void Activate()
    {
        view.OnMessageSubmitted += HandleMessageSubmitted;
        view.OnFocusChanged += HandleFocusChanged;
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

    private void HandleFocusChanged(bool isFocused)
    {
        OnFocusChanged?.Invoke(isFocused);
    }

    public void Dispose()
    {
        view.OnMessageSubmitted -= HandleMessageSubmitted;
        view.OnFocusChanged -= HandleFocusChanged;
        view.OnInputChanged -= OnInputChanged;
        chatEventBus.InsertTextInChat -= HandleExternalTextInsert;
    }

    public void UpdateStateForChannel(ChatChannel channel)
    {
        // Cancel any previous async operations for the old channel
        cts = cts.SafeRestart();

        // The logic is simple for non-user channels
        if (channel.ChannelType != ChatChannel.ChatChannelType.USER)
        {
            // view.SetState(InputState.Enabled);
            return;
        }

        // For DMs, we need to fetch the user's state asynchronously
        UpdateInputStateForUserAsync(channel.Id.Id, cts.Token).Forget();
    }

    private async UniTaskVoid UpdateInputStateForUserAsync(string userId, CancellationToken ct)
    {
        view.HideMask();
        view.SetInputEnabled(false);

        // Fetch the state from our service
        var result = await userStateUpdater.GetChatUserStateAsync(userId, ct)
            .SuppressCancellationThrow()
            .SuppressToResultAsync(ReportCategory.CHAT_MESSAGES);

        if (ct.IsCancellationRequested || !result.Success)
        {
            view.ShowMask("Could not retrieve user status.");
            return;
        }

        // Translate the model state from the service into a view state
        switch (result.Value.Result)
        {
            case ChatUserStateUpdater.ChatUserState.CONNECTED:
                view.HideMask();
                view.SetInputEnabled(true);
                break;

            case ChatUserStateUpdater.ChatUserState.BLOCKED_BY_OWN_USER:
                view.SetInputEnabled(false);
                view.ShowMask("To message this user you must first unblock them.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED_BY_OWN_USER:
                view.SetInputEnabled(false);
                view.ShowMask("Add this user as a friend to chat, or update your <b><u>DM settings</b></u> to connect with everyone.");
                break;

            case ChatUserStateUpdater.ChatUserState.PRIVATE_MESSAGES_BLOCKED:
                view.SetInputEnabled(false);
                view.ShowMask("The user you are trying to message only accepts DMs from friends.");
                break;

            case ChatUserStateUpdater.ChatUserState.DISCONNECTED:
            default:
                view.SetInputEnabled(false);
                view.ShowMask("The user you are trying to message is offline.");
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