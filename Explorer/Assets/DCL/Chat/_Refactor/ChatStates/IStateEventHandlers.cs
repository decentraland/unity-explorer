namespace  DCL.Chat.ChatStates
{
    public interface IClickInsideHandler
    {
        void OnClickInside();
    }

    public interface IClickOutsideHandler
    {
        void OnClickOutside();
    }

    public interface ICloseRequestHandler
    {
        void OnCloseRequested();
    }

    public interface IToggleMembersHandler
    {
        void OnToggleMembers(bool isVisible);
    }
    
    public interface IMinimizeRequestHandler
    {
        void OnMinimizeRequested();
    }
    
    public interface IFocusRequestHandler
    {
        void OnFocusRequested();
    }
}
