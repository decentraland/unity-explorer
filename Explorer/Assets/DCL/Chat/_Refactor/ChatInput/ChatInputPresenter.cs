using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Chat;
using DCL.Chat.ChatUseCases;
using DCL.Chat.EventBus;
using DCL.Chat.Services;
using DCL.Emoji;
using DCL.UI.Profiles.Helpers;
using MVC;
using Utility;
using Utility.Types;

public class ChatInputPresenter : IDisposable
{
    private readonly ChatInputView view;
    private readonly ICurrentChannelService currentChannelService;
    private readonly EventSubscriptionScope scope = new();

    private CancellationTokenSource cts = new ();

    private readonly MVCStateMachine<ChatInputState, ChatInputStateContext> fsm;

    public ChatInputPresenter(
        ChatInputView view,
        ChatConfig chatConfig,
        IEventBus eventBus,
        IChatEventBus chatEventBus,
        ICurrentChannelService currentChannelService,
        GetParticipantProfilesCommand getParticipantProfilesCommand,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SendMessageCommand sendMessageCommand)
    {
        this.view = view;
        this.currentChannelService = currentChannelService;

        var context = new ChatInputStateContext(view, view.inputEventBus, eventBus, getParticipantProfilesCommand, profileRepositoryWrapper, sendMessageCommand,
            new EmojiMapping(view.emojiContainer.emojiMappingJson, view.emojiContainer.emojiPanelConfiguration));

        fsm = new MVCStateMachine<ChatInputState, ChatInputStateContext>(context, new InitializingChatInputState());

        fsm.AddState(new HiddenChatInputState());
        fsm.AddState(new BlockedChatInputState(chatConfig, currentChannelService));
        fsm.AddState(new UnfocusedChatInputState());
        fsm.AddState(new TypingEnabledChatInputState(chatEventBus));

        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
        scope.Add(eventBus.Subscribe<ChatEvents.CurrentChannelStateUpdatedEvent>(OnForceRefreshInputState));
    }

    public void ShowUnfocused()
    {
        fsm.ChangeState<UnfocusedChatInputState>();
    }

    public void Hide()
    {
        fsm.ChangeState<HiddenChatInputState>();
    }

    public async UniTaskVoid ShowFocusedAsync()
    {
        cts = cts.SafeRestart();

        fsm.ChangeState<InitializingChatInputState>();

        Result<ChatUserStateUpdater.ChatUserState> result = await currentChannelService.ResolveInputStateAsync(cts.Token);
        OnBlockedUpdated(result);
    }

    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
        view.ClearInput();

        UpdateStateForChannel().Forget();
    }

    private void OnForceRefreshInputState(ChatEvents.CurrentChannelStateUpdatedEvent evt)
    {
        UpdateStateForChannel().Forget();
    }

    public void OnBlur()
    {
        cts.Cancel();
        fsm.ChangeState<UnfocusedChatInputState>();
    }

    public void OnMinimize()
    {
        cts.Cancel();
        fsm.ChangeState<UnfocusedChatInputState>();
    }

    private async UniTaskVoid UpdateStateForChannel()
    {
        cts = cts.SafeRestart();

        Result<ChatUserStateUpdater.ChatUserState> result = await currentChannelService.ResolveInputStateAsync(cts.Token);
        OnBlockedUpdated(result);
    }

    private void OnBlockedUpdated(Result<ChatUserStateUpdater.ChatUserState> result)
    {
        fsm.CurrentState.OnBlockedUpdated(result is { Success: true, Value: ChatUserStateUpdater.ChatUserState.CONNECTED });
    }

    public void Dispose()
    {
        scope.Dispose();
        cts.Cancel();
        cts.Dispose();
        fsm.Dispose();
    }
}
