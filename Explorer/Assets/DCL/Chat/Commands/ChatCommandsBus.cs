using CodeLess.Attributes;
using System;

namespace DCL.Chat.Commands
{
    /// <summary>
    ///     This bus is used by chat commands, to avoid sending references to classes they should not have, like specific controllers or views
    ///     Just send this interface to both ends, subscribe to the event in the controller/etc. and wait for the event to be raised. Just like magic.
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
