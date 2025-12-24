using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.EventBus;
using DCL.Emoji;
using DCL.UI.InputFieldFormatting;
using DCL.UI.Profiles.Helpers;
using DCL.Utility.Types;
using MVC;
using System;
using System.Threading;
using Utility;

namespace DCL.Chat.ChatInput
{
    public class ChatInputPresenter : IDisposable
    {
        private readonly ChatInputView view;
        private readonly ResolveInputStateCommand resolveInputStateCommand;
        private readonly EventSubscriptionScope scope = new ();

        private CancellationTokenSource cts = new ();

        private readonly MVCStateMachine<ChatInputState, ChatInputStateContext> fsm;

        public ChatInputPresenter(
            ChatInputView view,
            ChatConfig.ChatConfig chatConfig,
            IEventBus eventBus,
            IChatEventBus chatEventBus,
            CurrentChannelService currentChannelService,
            ResolveInputStateCommand resolveInputStateCommand,
            GetParticipantProfilesCommand getParticipantProfilesCommand,
            ProfileRepositoryWrapper profileRepositoryWrapper,
            SendMessageCommand sendMessageCommand,
            ITextFormatter textFormatter)
        {
            this.view = view;
            this.view.Initialize(chatConfig, textFormatter);

            this.resolveInputStateCommand = resolveInputStateCommand;

            var context = new ChatInputStateContext();
            fsm = new MVCStateMachine<ChatInputState, ChatInputStateContext>(
                context,
                states: new ChatInputState[]
                {
                    new InitializingChatInputState(),
                    new HiddenChatInputState(view),
                    new BlockedChatInputState(view, eventBus, chatConfig, currentChannelService),
                    new UnfocusedChatInputState(view, eventBus),
                    new TypingEnabledChatInputState(view,
                        chatEventBus,
                        sendMessageCommand,
                        new EmojiMapping(view.emojiContainer.emojiPanelConfiguration),
                        profileRepositoryWrapper,
                        getParticipantProfilesCommand
                    ),
                }
            );

            fsm.Enter<InitializingChatInputState>();

            scope.Add(eventBus.Subscribe<ChatEvents.ChannelSelectedEvent>(OnChannelSelected));
            scope.Add(eventBus.Subscribe<ChatEvents.CurrentChannelStateUpdatedEvent>(OnForceRefreshInputState));
            scope.Add(eventBus.Subscribe<ChatEvents.ChatResetEvent>(OnChatReset));
        }

        private void OnChatReset(ChatEvents.ChatResetEvent obj)
        {
            cts.SafeCancelAndDispose();

            view.ClearInput();

            fsm.Enter<UnfocusedChatInputState>();
        }

        public void ShowUnfocused()
        {
            fsm.Enter<UnfocusedChatInputState>();
        }

        public void Hide()
        {
            fsm.Enter<HiddenChatInputState>();
        }

        public async UniTaskVoid ShowFocusedAsync()
        {
            cts = cts.SafeRestart();

            fsm.Enter<InitializingChatInputState>();

            Result<PrivateConversationUserStateService.ChatUserState> result = await resolveInputStateCommand.ExecuteAsync(cts.Token);
            OnBlockedUpdated(result);
        }

        private void OnChannelSelected(ChatEvents.ChannelSelectedEvent evt)
        {
            view.ClearInput();

            UpdateStateForChannelAsync().Forget();
        }

        private void OnForceRefreshInputState(ChatEvents.CurrentChannelStateUpdatedEvent evt)
        {
            if (fsm.CurrentState is not UnfocusedChatInputState)
                UpdateStateForChannelAsync().Forget();
        }

        public void OnBlur()
        {
            cts.SafeCancelAndDispose();
            fsm.Enter<UnfocusedChatInputState>();
        }

        public void OnMinimize()
        {
            cts.SafeCancelAndDispose();
            fsm.Enter<UnfocusedChatInputState>();
        }

        private async UniTaskVoid UpdateStateForChannelAsync()
        {
            cts = cts.SafeRestart();

            Result<PrivateConversationUserStateService.ChatUserState> result = await resolveInputStateCommand.ExecuteAsync(cts.Token);
            OnBlockedUpdated(result);
        }

        private void OnBlockedUpdated(Result<PrivateConversationUserStateService.ChatUserState> result)
        {
            fsm.CurrentState.OnBlockedUpdated(result is { Success: true, Value: PrivateConversationUserStateService.ChatUserState.CONNECTED });
        }

        public void Dispose()
        {
            scope.Dispose();
            cts.SafeCancelAndDispose();
            fsm.Dispose();
        }
    }
}
