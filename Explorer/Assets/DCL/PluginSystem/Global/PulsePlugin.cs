using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pulse;
using Decentraland.Pulse;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class PulsePlugin : IDCLGlobalPlugin<PulsePlugin.PulseSettings>
    {
        private readonly ITransport transport;
        private readonly PulseMultiplayerService service;
        private readonly CancellationTokenSource lifeCycleCts = new ();

        public PulsePlugin(ITransport transport,
            PulseMultiplayerService service)
        {
            this.transport = transport;
            this.service = service;
        }

        public void Dispose()
        {
            lifeCycleCts.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(PulseSettings settings, CancellationToken ct)
        {
            var uriBuilder = new UriBuilder
            {
                Port = settings.Port,
                Host = settings.Address,
            };

            await transport.ConnectAsync(uriBuilder.Uri, ct);
            transport.ListenForIncomingDataAsync(lifeCycleCts.Token).Forget();
            service.RouteIncomingMessagesAsync(lifeCycleCts.Token).Forget();
            HandleHandshakeAsync(lifeCycleCts.Token).Forget();
        }

        private async UniTaskVoid HandleHandshakeAsync(CancellationToken ct)
        {
            await foreach (HandshakeResponse handshakeResponse in service.SubscribeAsync<HandshakeResponse>(ServerMessage.MessageOneofCase.Handshake, lifeCycleCts.Token)) { }
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
