using MVC;
using UnityEngine.EventSystems;

namespace DCL.Chat.ChatInput
{
    public class UnfocusedChatInputState : ChatInputState, IState
    {
        private readonly MVCStateMachine<ChatInputState> stateMachine;
        private readonly ChatInputView view;
        private readonly ChatEventBus eventBus;

        public UnfocusedChatInputState(MVCStateMachine<ChatInputState> stateMachine, ChatInputView view, ChatEventBus eventBus)
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

            // Another view's input field may already own the selection — never clear one the chat doesn't hold
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == view.inputField.gameObject)
                EventSystem.current.SetSelectedGameObject(null);

            view.inputField.onSelect.AddListener(OnInputSelected);
        }

        private void OnInputSelected(string _)
        {
            // It's a global event as we need to switch the state of the whole Chat View
            // Switching the state of the Chat View will lead to switching the state of the Chat Input
            eventBus.RaiseFocusRequestedEvent();
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
