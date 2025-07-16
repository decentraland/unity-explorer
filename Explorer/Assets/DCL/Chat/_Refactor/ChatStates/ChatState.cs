using DCL.Chat._Refactor.ChatStates;
using MVC;

namespace DCL.Chat
{
    public class ChatState : MVCState<ChatState, ChatStateContext>
    {
        public virtual void OnClickOutside() { }

        public virtual void OnClickInside() { }

        public virtual void OnCloseRequested() { }

        public virtual void OnFocusRequested() { }

        public virtual void OnMinimizeRequested() { }

        public virtual void OnToggleMembers() { }
    }
}
