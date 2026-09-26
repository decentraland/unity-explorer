using CodeLess.Attributes;
using System;

namespace DCL.Chat.Commands
{
    /// <summary>
    ///     This bus is used by chat commands, to avoid sending references to classes they should not have
    ///     TODO: This is probably outdated and can be replaced at this point.
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class ChatCommandsBus
    {
        public event Action<bool> ConnectionStatusPanelVisibilityChanged;

        public void SendConnectionStatusPanelChangedNotification(bool isVisible)
        {
            ConnectionStatusPanelVisibilityChanged?.Invoke(isVisible);
        }
    }
}
