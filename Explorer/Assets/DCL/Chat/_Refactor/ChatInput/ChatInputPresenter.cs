using Cysharp.Threading.Tasks;
using DCL.Chat.ChatCommands;
using DCL.Chat.ChatServices;
using DCL.Chat.EventBus;
using DCL.Emoji;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Threading;
using Utility;
using Utility.Types;

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
            SendMessageCommand sendMessageCommand)
        {
            this.view = view;
            this.view.Initialize(chatConfig);
            
            this.resolveInputStateCommand = resolveInputStateCommand;

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
            cts.Cancel();
            fsm.ChangeState<UnfocusedChatInputState>();
        }

        public void OnMinimize()
        {
            cts.Cancel();
            fsm.ChangeState<UnfocusedChatInputState>();
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
            cts.Cancel();
            cts.Dispose();
            fsm.Dispose();
        }
    }
}
