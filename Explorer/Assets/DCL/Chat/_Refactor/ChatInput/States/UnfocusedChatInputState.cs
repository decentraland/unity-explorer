using MVC;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Chat.ChatInput
{
    public class UnfocusedChatInputState : ChatInputState, IState
    {
        private readonly MVCStateMachine<ChatInputState> stateMachine;
        private readonly ChatInputView view;
        private readonly IEventBus eventBus;

        public UnfocusedChatInputState(MVCStateMachine<ChatInputState> stateMachine, ChatInputView view, IEventBus eventBus)
        {
            this.stateMachine = stateMachine;
            this.view = view;
            this.eventBus = eventBus;
        }

        public void Enter()
        {
            view.Show();
            view.SetDefault();
            view.RefreshHeight();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            view.inputField.onSelect.AddListener(OnInputSelected);
        }

        private void OnInputSelected(string _)
        {
            // It's a global event as we need to switch the state of the whole Chat View
            // Switching the state of the Chat View will lead to switching the state of the Chat Input
            eventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }

        public override void Exit()
        {
            view.inputField.onSelect.RemoveListener(OnInputSelected);
        }

        protected override void OnInputBlocked()
        {
            stateMachine.Enter<BlockedChatInputState>();
        }
    }
}
