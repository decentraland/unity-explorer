using System;
using UnityEngine.EventSystems;
using Utility;

namespace DCL.Chat.ChatInput
{
    public class UnfocusedChatInputState : ChatInputState
    {
        private readonly ChatInputView view;
        private readonly IEventBus eventBus;

        public UnfocusedChatInputState(ChatInputView view, IEventBus eventBus)
        {
            this.view = view;
            this.eventBus = eventBus;
        }

        public override void Enter()
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
            machine.Enter<BlockedChatInputState>();
        }
    }
}
