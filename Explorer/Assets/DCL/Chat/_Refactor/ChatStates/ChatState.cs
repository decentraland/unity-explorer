using MVC;

namespace DCL.Chat.ChatStates
{
    public abstract class ChatState : IState
    {
        public virtual void Enter() { }

        public virtual void Exit() { }

        public virtual void OnClickOutside() { }

        public virtual void OnClickInside() { }

        public virtual void OnCloseRequested() { }

        public virtual void OnFocusRequested() { }

        public virtual void OnMinimizeRequested() { }

        public virtual void OnToggleMembers() { }

        public virtual void OnPointerEnter() { }

        public virtual void OnPointerExit() { }
    }
}
