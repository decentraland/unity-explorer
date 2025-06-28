using System;

namespace DCL.Chat
{
    public interface IChatTitlebarView
    {
        event Action OnCloseClicked;
        event Action<bool> OnMemberListToggled;
    }
}