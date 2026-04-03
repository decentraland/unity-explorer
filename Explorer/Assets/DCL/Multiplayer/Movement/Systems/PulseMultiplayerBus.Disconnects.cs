using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Pulse.Transport;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private const int RECONNECTION_DELAY_MS = 10000;

        private async UniTask MonitorDisconnectsAsync(CancellationToken ct)
        {
            await foreach (DisconnectReason reason in pulseService.ReadDisconnectsAsync(ct))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse transport disconnected: {reason}");

                pulseService.ResetConnectionLifecycle();

                // The grace period for peers removal should be determined by the transport timeout itself
                RemoveAllPeers();

                if (reason is not (DisconnectReason.NONE or DisconnectReason.GRACEFUL)) continue;

                ReportHub.Log(ReportCategory.MULTIPLAYER, "Attempting reconnection...");

                await UniTask.Delay(RECONNECTION_DELAY_MS, cancellationToken: ct);

                try { await pulseService.ConnectAsync(ct); }
                catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.MULTIPLAYER); }
            }
        }
    }
}
