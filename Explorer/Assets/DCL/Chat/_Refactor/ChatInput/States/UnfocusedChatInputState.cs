using UnityEngine.EventSystems;

namespace DCL.Chat.ChatInput
{
    public class UnfocusedChatInputState : ChatInputState
    {
        public override void Begin()
        {
            context.ChatInputView.Show();
            context.ChatInputView.SetDefault();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            
            context.ChatInputView.inputField.onSelect.AddListener(OnInputSelected);
        }

        private void OnInputSelected(string _)
        {
            // It's a global event as we need to switch the state of the whole Chat View
            // Switching the state of the Chat View will lead to switching the state of the Chat Input
            context.ChatEventBus.Publish(new ChatEvents.FocusRequestedEvent());
        }

        public override void End()
        {
            context.ChatInputView.inputField.onSelect.RemoveListener(OnInputSelected);
        }

        protected override void OnInputBlocked()
        {
            ChangeState<BlockedChatInputState>();
        }
    }
}
