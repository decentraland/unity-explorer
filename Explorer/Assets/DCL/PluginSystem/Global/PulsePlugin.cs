using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class PulsePlugin : IDCLGlobalPlugin<PulsePlugin.PulseSettings>
    {
        private readonly PulseMultiplayerService service;
        private readonly CancellationTokenSource lifeCycleCts = new ();

        public PulsePlugin(PulseMultiplayerService service)
        {
            this.service = service;
        }

        public void Dispose()
        {
            lifeCycleCts.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PulseSettings settings, CancellationToken ct)
        {
            await service.ConnectAsync(settings.Address, settings.Port, CancellationTokenSource.CreateLinkedTokenSource(lifeCycleCts.Token, ct).Token);
        }

        public class PulseSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public string Address { get; set; }
            [field: SerializeField]
            public int Port { get; set; }
        }
    }
}
