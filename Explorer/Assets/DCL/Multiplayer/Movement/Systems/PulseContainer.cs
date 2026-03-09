using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Pulse.ENet;
using DCL.Multiplayer.Movement;
using DCL.PluginSystem;
using DCL.Web3.Identities;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseContainer : DCLWorldContainer<PulseContainer.Settings>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly MovementInbox movementInbox;

        private readonly PeerIdCache peerIdCache = new ();
        private readonly MessagePipe messagePipe = new ();

        private ENetTransport? transport;
        private PulseMultiplayerBus? pulseMultiplayerBus;
        private CancellationTokenSource? lifeCycleCts;

        public PulseContainer(IWeb3IdentityCache identityCache, MovementInbox movementInbox)
        {
            this.identityCache = identityCache;
            this.movementInbox = movementInbox;
        }

        internal PulseMultiplayerService? pulseMultiplayerService { get; private set; }

        public static async UniTask<PulseContainer> CreateAsync(IPluginSettingsContainer pluginSettingsContainer,
            IWeb3IdentityCache web3IdentityCache, MovementInbox movementInbox,
            CancellationToken ct)
        {
            var container = new PulseContainer(web3IdentityCache, movementInbox);
            await container.InitializeContainerAsync<PulseContainer, Settings>(pluginSettingsContainer, ct);
            return container;
        }

        protected override UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            lifeCycleCts = lifeCycleCts.SafeRestart();

            transport = new ENetTransport(settings.ENetTransportOptions, messagePipe);
            pulseMultiplayerService = new PulseMultiplayerService(transport, messagePipe, identityCache);

            pulseMultiplayerBus = new PulseMultiplayerBus(pulseMultiplayerService, peerIdCache, movementInbox);
            pulseMultiplayerBus.SubscribeToIncomingMessages(lifeCycleCts.Token);

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            lifeCycleCts?.SafeCancelAndDispose();
            pulseMultiplayerBus?.Dispose();
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public ENetTransportOptions ENetTransportOptions { get; private set; }
        }
    }
}
