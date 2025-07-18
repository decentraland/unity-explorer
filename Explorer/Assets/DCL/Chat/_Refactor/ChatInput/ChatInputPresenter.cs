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
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine.TextCore.Text;
using Utilities;
using Utility;
using Utility.Types;
using TextAsset = UnityEngine.TextAsset;

public class ChatInputPresenter : IDisposable
{
    private readonly ICurrentChannelService currentChannelService;
    private readonly EventSubscriptionScope scope = new();

    private CancellationTokenSource cts = new ();

    private readonly MVCStateMachine<ChatInputState, ChatInputStateContext> fsm;

    public ChatInputPresenter(
        ChatInputView view,
        ChatConfig chatConfig,
        IEventBus eventBus,
        ICurrentChannelService currentChannelService,
        GetParticipantProfilesCommand getParticipantProfilesCommand,
        ProfileRepositoryWrapper profileRepositoryWrapper,
        SendMessageCommand sendMessageCommand)
    {
        this.currentChannelService = currentChannelService;

        var context = new ChatInputStateContext(view, view.inputEventBus, eventBus, getParticipantProfilesCommand, profileRepositoryWrapper, sendMessageCommand,
            CreateEmojiMapping(view.emojiMappingJson, view.emojiPanelConfiguration));

        fsm = new MVCStateMachine<ChatInputState, ChatInputStateContext>(context, new InitializingChatInputState());

        fsm.AddState(new HiddenChatInputState());
        fsm.AddState(new BlockedChatInputState(chatConfig, currentChannelService));
        fsm.AddState(new UnfocusedChatInputState());
        fsm.AddState(new TypingEnabledChatInputState());

        scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
        scope.Add(eventBus.Subscribe<ChatEvents.CurrentChannelStateUpdatedEvent>(OnForceRefreshInputState));
    }

    private Dictionary<string, EmojiData> CreateEmojiMapping(TextAsset emojiMappingJson, EmojiPanelConfigurationSO emojiPanelConfiguration)
    {
        Dictionary<string, EmojiData> emojiNameMapping = new ();

        foreach (KeyValuePair<string, string> emojiData in JsonConvert.DeserializeObject<Dictionary<string, string>>(emojiMappingJson.text))
        {
            if (emojiPanelConfiguration.SpriteAsset.GetSpriteIndexFromName(emojiData.Value.ToUpper()) == -1)
                continue;

            emojiNameMapping.Add(emojiData.Key, new EmojiData($"\\U000{emojiData.Value.ToUpper()}", emojiData.Key));
        }

        return emojiNameMapping;
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

        if (result is { Success: true, Value: ChatUserStateUpdater.ChatUserState.CONNECTED })
            fsm.ChangeState<TypingEnabledChatInputState>();
        else
            fsm.ChangeState<BlockedChatInputState>();
    }

    private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
    {
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
