using Cysharp.Threading.Tasks;
using DCL.FeatureFlags;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Connections.Pulse.ENet;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.RemoveIntentions;
using DCL.PluginSystem;
using DCL.Profiles.Self;
using DCL.Web3.Identities;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.Multiplayer.Movement
{
    internal class PulseContainer : DCLWorldContainer<PulseContainer.Settings>
    {
        private readonly IWeb3IdentityCache identityCache;
        private readonly MovementInbox movementInbox;
        private readonly PeerIdCache peerIdCache = new ();
        private readonly MessagePipe messagePipe = new ();

        internal ENetTransport? transport;
        private CancellationTokenSource? lifeCycleCts;

        internal readonly ParcelEncoder parcelEncoder;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;

        public readonly PulseIncomingProfileAnnouncements IncomingProfiles;
        public readonly PulseRemoveIntentions RemoveIntentions;

        public readonly bool FeatureEnabled;

        internal PulseMultiplayerBus? pulseMultiplayerBus { get; private set; }
        internal IPulseMultiplayerService? pulseMultiplayerService { get; private set; }
        internal IProfilePropagation? pulseProfilePropagationBus { get; private set; }

        private PulseContainer(IWeb3IdentityCache identityCache, MovementInbox movementInbox,
            ParcelEncoder parcelEncoder, IDecentralandUrlsSource urlsSource, ISelfProfile selfProfile, IRealmData realmData)
        {
            this.identityCache = identityCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
            this.urlsSource = urlsSource;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            IncomingProfiles = new PulseIncomingProfileAnnouncements();
            RemoveIntentions = new PulseRemoveIntentions();

            FeatureEnabled = FeaturesRegistry.Instance.IsEnabled(FeatureId.PULSE);
        }

        public static async UniTask<PulseContainer> CreateAsync(
            IPluginSettingsContainer pluginSettingsContainer,
            IWeb3IdentityCache web3IdentityCache,
            MovementInbox movementInbox,
            LandscapeData landscapeData,
            IDecentralandUrlsSource urlsSource,
            ISelfProfile selfProfile,
            IRealmData realmData,
            CancellationToken ct)
        {
            var container = new PulseContainer(web3IdentityCache, movementInbox, new ParcelEncoder(landscapeData.terrainData), urlsSource, selfProfile, realmData);
            await container.InitializeContainerAsync<PulseContainer, Settings>(pluginSettingsContainer, ct);
            return container;
        }

        protected override UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            lifeCycleCts = lifeCycleCts.SafeRestart();

            transport = new ENetTransport(settings.ENetTransportOptions, messagePipe);
            pulseMultiplayerService = FeatureEnabled ? new PulseMultiplayerService(transport, messagePipe, urlsSource) : new IPulseMultiplayerService.Dummy();

            pulseMultiplayerBus = new PulseMultiplayerBus(pulseMultiplayerService, peerIdCache, movementInbox,
                parcelEncoder, IncomingProfiles, RemoveIntentions, identityCache, settings.ReconnectionSettings, selfProfile, realmData);
            pulseMultiplayerBus.SubscribeToIncomingMessages();

            pulseProfilePropagationBus = FeatureEnabled ? new PulseProfilePropagationBus(pulseMultiplayerService) : new IProfilePropagation.Dummy();

            return UniTask.CompletedTask;
        }

        public override void Dispose()
        {
            lifeCycleCts?.SafeCancelAndDispose();
            pulseMultiplayerBus?.Dispose();
            pulseMultiplayerService?.Dispose();
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public ENetTransportOptions ENetTransportOptions { get; private set; }

            [field: SerializeField]
            public PulseMultiplayerBus.ReconnectionSettings ReconnectionSettings { get; private set; }
        }
    }
}
