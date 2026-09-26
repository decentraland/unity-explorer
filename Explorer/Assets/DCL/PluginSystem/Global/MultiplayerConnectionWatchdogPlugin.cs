using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.WebRequests;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Wires <see cref="MultiplayerConnectionWatchdog" /> into the global world lifetime so the
    ///     connection-lost popup can be triggered in-world (not only during the start/teleport flow).
    /// </summary>
    public class MultiplayerConnectionWatchdogPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MultiplayerConnectionWatchdog watchdog;

        public MultiplayerConnectionWatchdogPlugin(
            IRoomHub roomHub,
            ITransport pulseTransport,
            IWebRequestController webRequestController,
            IMVCManager mvcManager,
            IDecentralandUrlsSource decentralandUrlsSource)
        {
            watchdog = new MultiplayerConnectionWatchdog(roomHub, pulseTransport, webRequestController, mvcManager, decentralandUrlsSource);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct)
        {
            watchdog.Start();
            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            watchdog.Dispose();
        }
    }
}
