using Cysharp.Threading.Tasks;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.Pulse.ENet;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Multiplayer.Profiles.RemoteAnnouncements;
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
        private CancellationTokenSource? lifeCycleCts;

        internal readonly ParcelEncoder parcelEncoder;
        private readonly PulseIncomingProfileAnnouncements incomingProfiles;

        internal PulseMultiplayerBus? pulseMultiplayerBus { get; private set; }
        internal PulseMultiplayerService? pulseMultiplayerService { get; private set; }
        internal PulseProfilePropagationBus? pulseProfilePropagationBus { get; private set; }

        private PulseContainer(IWeb3IdentityCache identityCache, MovementInbox movementInbox, ParcelEncoder parcelEncoder,
            PulseIncomingProfileAnnouncements incomingProfiles)
        {
            this.identityCache = identityCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.incomingProfiles = incomingProfiles;
        }

        public static async UniTask<PulseContainer> CreateAsync(IPluginSettingsContainer pluginSettingsContainer,
            IWeb3IdentityCache web3IdentityCache, MovementInbox movementInbox, LandscapeData landscapeData,
            PulseIncomingProfileAnnouncements incomingProfiles,
            CancellationToken ct)
        {
            var container = new PulseContainer(web3IdentityCache, movementInbox, new ParcelEncoder(landscapeData.terrainData), incomingProfiles);
            await container.InitializeContainerAsync<PulseContainer, Settings>(pluginSettingsContainer, ct);
            return container;
        }

        protected override UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            lifeCycleCts = lifeCycleCts.SafeRestart();

            transport = new ENetTransport(settings.ENetTransportOptions, messagePipe);
            pulseMultiplayerService = new PulseMultiplayerService(transport, messagePipe, identityCache);

            pulseMultiplayerBus = new PulseMultiplayerBus(pulseMultiplayerService, peerIdCache, movementInbox, parcelEncoder, incomingProfiles);
            pulseMultiplayerBus.SubscribeToIncomingMessages(lifeCycleCts.Token);

            pulseProfilePropagationBus = new PulseProfilePropagationBus(pulseMultiplayerService);

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

            [field: SerializeField]
            public LandscapeDataRef LandscapeData { get; private set; }
        }
    }
}
