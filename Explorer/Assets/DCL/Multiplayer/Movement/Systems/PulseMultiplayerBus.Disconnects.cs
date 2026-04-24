using DCL.Diagnostics;
using Pulse.Transport;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        [Serializable]
        public class ReconnectionSettings
        {
            [field: SerializeField]
            public int DefaultRetryDelayMs { get; private set; } = 3000;

            [field: SerializeField]
            public int AuthExhaustionRetryDelayMs { get; private set; } = 10000;

            [field: SerializeField]
            public int ServerFullRetryDelayMs { get; private set; } = 60000;
        }

        private readonly ReconnectionSettings settings;

        private (bool reconnectionAllowed, TimeSpan reconnectionDelay) HandleDisconnect(DisconnectReason reason)
        {
            ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse transport disconnected: {reason}");

            RemoveAllPeers();

            return reason switch
                   {
                       DisconnectReason.GRACEFUL or DisconnectReason.AUTH_TIMEOUT or DisconnectReason.AUTH_FAILED => (true, TimeSpan.FromMilliseconds(settings.DefaultRetryDelayMs)),
                       DisconnectReason.SERVER_FULL => (true, TimeSpan.FromMilliseconds(settings.ServerFullRetryDelayMs)),
                       DisconnectReason.PRE_AUTH_BUDGET_EXHAUSTED or DisconnectReason.PRE_AUTH_IP_LIMIT_EXHAUSTED => (true, TimeSpan.FromMilliseconds(settings.AuthExhaustionRetryDelayMs)),
                       _ => (false, TimeSpan.Zero),
                   };
        }
    }
}
