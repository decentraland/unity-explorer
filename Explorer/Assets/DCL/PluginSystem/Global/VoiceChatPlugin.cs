using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using LiveKit.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.UI.Profiles.Helpers;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.VoiceChat.Proximity;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Concurrent;
using System.Threading;
using DCL.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;
using AudioSettings = UnityEngine.AudioSettings;
using RustAudio;

namespace DCL.PluginSystem.Global
{
    public class VoiceChatPlugin : IDCLGlobalPlugin<VoiceChatPlugin.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainer;
        private readonly IRoomHub roomHub;
        private readonly VoiceChatPanelView voiceChatPanelView;
        private readonly ProfileRepositoryWrapper profileDataProvider;
        private readonly CommunitiesDataProvider communityDataProvider;
        private readonly ImageControllerProvider  imageControllerProvider;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly VoiceChatOrchestrator voiceChatOrchestrator;
        private readonly ChatSharedAreaEventBus chatSharedAreaEventBus;
        private readonly EventSubscriptionScope pluginScope = new ();
        private readonly ConcurrentDictionary<string, AudioSource> proximityAudioSources = new ();
        private readonly ProximityMuteService proximityMuteService;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ProximityVoiceChatButtonView? proximityVoiceChatButtonView;
        private readonly NearbyVoiceWidgetView? nearbyVoiceWidgetView;
        private readonly ProximityConfigHolder proximityConfigHolder = new ();

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private VoiceChatTrackManager? trackManager;
        private VoiceChatRoomManager? roomManager;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private MicrophoneAudioToggleHandler? microphoneAudioToggleHandler;
        private VoiceChatPanelPresenter? voiceChatPanelPresenter;
        private VoiceChatDebugContainer? voiceChatDebugContainer;
        private ProximityVoiceChatManager? proximityVoiceChatManager;
        private ProximityNametagsHandler? proximityNametagsHandler;
        private ProximityVoiceChatStateModel? proximityStateModel;
        private ProximityVoiceChatButtonController? proximityButtonController;
        private NearbyVoiceWidgetController? nearbyVoiceWidgetController;
        private VoiceChatConfiguration? storedVoiceChatConfig;
        private Action? activeSpeakersUpdatedHandler;

        public VoiceChatPlugin(
            IRoomHub roomHub,
            VoiceChatPanelView voiceChatPanelView,
            VoiceChatContainer voiceChatContainer,
            ProfileRepositoryWrapper profileDataProvider,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            Arch.Core.World world,
            Entity playerEntity,
            CommunitiesDataProvider communityDataProvider,
            ImageControllerProvider imageControllerProvider,
            IAssetsProvisioner assetsProvisioner,
            ChatSharedAreaEventBus chatSharedAreaEventBus,
            IDebugContainerBuilder debugContainer,
            ProximityMuteService proximityMuteService,
            IWeb3IdentityCache identityCache,
            ProximityVoiceChatButtonView? proximityVoiceChatButtonView,
            NearbyVoiceWidgetView? nearbyVoiceWidgetView)
        {
            this.roomHub = roomHub;
            this.voiceChatPanelView = voiceChatPanelView;
            this.profileDataProvider = profileDataProvider;
            this.entityParticipantTable = entityParticipantTable;
            this.world = world;
            this.playerEntity = playerEntity;
            this.communityDataProvider = communityDataProvider;
            this.imageControllerProvider = imageControllerProvider;
            this.assetsProvisioner = assetsProvisioner;
            this.chatSharedAreaEventBus = chatSharedAreaEventBus;
            this.debugContainer = debugContainer;
            this.proximityMuteService = proximityMuteService;
            this.identityCache = identityCache;
            this.proximityVoiceChatButtonView = proximityVoiceChatButtonView;
            this.nearbyVoiceWidgetView = nearbyVoiceWidgetView;

            voiceChatOrchestrator = voiceChatContainer.VoiceChatOrchestrator;
        }

        public void Dispose()
        {
            identityCache.OnIdentityChanged -= OnProximityIdentityAvailable;

            if (activeSpeakersUpdatedHandler != null)
            {
                roomHub.IslandRoom().ActiveSpeakers.Updated -= activeSpeakersUpdatedHandler;
                activeSpeakersUpdatedHandler = null;
                proximityConfigHolder.SpeakingParticipants.Clear();
            }

            pluginScope.Dispose();

            if (voiceChatPluginSettingsAsset.Value != null)
                voiceChatPluginSettingsAsset.Dispose();

            RustAudioClient.DeInit();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ProximityAudioDebugWidget.Setup(debugContainer, proximityConfigHolder);
            ProximityAudioPositionSystem.InjectToWorld(ref builder, entityParticipantTable, proximityAudioSources, proximityConfigHolder);
            ProximityLipSyncSystem.InjectToWorld(ref builder, entityParticipantTable, proximityAudioSources, proximityConfigHolder);
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "VOICE CHAT!");
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatPluginSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);

            VoiceChatPluginSettings pluginSettings = voiceChatPluginSettingsAsset.Value;
            VoiceChatConfiguration voiceChatConfiguration = pluginSettings.VoiceChatConfiguration;

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration, voiceChatOrchestrator);
            pluginScope.Add(voiceChatHandler);

            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);
            pluginScope.Add(microphoneStateManager);

            trackManager = new VoiceChatTrackManager(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, voiceChatHandler);
            pluginScope.Add(trackManager);

            roomManager = new VoiceChatRoomManager(trackManager, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager);
            pluginScope.Add(roomManager);

            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator,
                entityParticipantTable,
                world,
                playerEntity);

            pluginScope.Add(nametagsHandler);

            VoiceChatParticipantEntryView playerEntry = pluginSettings.PlayerEntryView;
            AudioClipConfig muteMicrophoneAudio = pluginSettings.MuteMicrophoneAudio;
            AudioClipConfig unmuteMicrophoneAudio = pluginSettings.UnmuteMicrophoneAudio;
            microphoneAudioToggleHandler = new MicrophoneAudioToggleHandler(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);
            pluginScope.Add(microphoneAudioToggleHandler);

            voiceChatPanelPresenter = new VoiceChatPanelPresenter(voiceChatPanelView, profileDataProvider, communityDataProvider, imageControllerProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, roomHub, playerEntry, chatSharedAreaEventBus);
            pluginScope.Add(voiceChatPanelPresenter);

            voiceChatDebugContainer = new VoiceChatDebugContainer(debugContainer, trackManager);
            pluginScope.Add(voiceChatDebugContainer);

            proximityConfigHolder.Config = voiceChatConfiguration;

            if (voiceChatConfiguration.MouthAtlasTexture != null)
                proximityConfigHolder.MouthTextureArray = SliceMouthAtlas(voiceChatConfiguration.MouthAtlasTexture, 4, 4);

            IRoom islandRoom = roomHub.IslandRoom();

            var activeSpeakers = islandRoom.ActiveSpeakers;
            activeSpeakersUpdatedHandler = () =>
            {
                proximityConfigHolder.SpeakingParticipants.Clear();

                foreach (string identity in activeSpeakers)
                    proximityConfigHolder.SpeakingParticipants.Add(identity);
            };
            activeSpeakers.Updated += activeSpeakersUpdatedHandler;

            storedVoiceChatConfig = voiceChatConfiguration;
            identityCache.OnIdentityChanged += OnProximityIdentityAvailable;

            if (identityCache.Identity != null)
                await InitializeProximityAsync(ct);
        }

        private void OnProximityIdentityAvailable()
        {
            if (identityCache.Identity == null) return;
            if (proximityNametagsHandler != null) return;

            InitializeProximityAsync(CancellationToken.None).Forget();
        }

        private async UniTask InitializeProximityAsync(CancellationToken ct)
        {
            await proximityMuteService.LoadAsync(ct);

            string localIdentity = identityCache.Identity!.Address.ToString();

            proximityStateModel = new ProximityVoiceChatStateModel();
            pluginScope.Add(proximityStateModel);

            proximityNametagsHandler = new ProximityNametagsHandler(
                roomHub.IslandRoom(), entityParticipantTable, world,
                voiceChatOrchestrator.CurrentCallStatus, playerEntity,
                localIdentity, proximityMuteService, proximityStateModel);
            pluginScope.Add(proximityNametagsHandler);

            proximityVoiceChatManager = new ProximityVoiceChatManager(
                roomHub.IslandRoom(), storedVoiceChatConfig!,
                proximityAudioSources, voiceChatOrchestrator.CurrentCallStatus,
                proximityMuteService, proximityStateModel);
            pluginScope.Add(proximityVoiceChatManager);

            if (proximityVoiceChatButtonView != null)
            {
                proximityButtonController = new ProximityVoiceChatButtonController(
                    proximityVoiceChatButtonView, proximityStateModel);
                pluginScope.Add(proximityButtonController);
            }

            if (nearbyVoiceWidgetView != null)
            {
                nearbyVoiceWidgetController = new NearbyVoiceWidgetController(
                    nearbyVoiceWidgetView, proximityStateModel,
                    storedVoiceChatConfig!.ProximityChatAudioMixerGroup);
                pluginScope.Add(nearbyVoiceWidgetController);
            }
        }

        private static Texture2DArray SliceMouthAtlas(Texture2D atlas, int cols, int rows)
        {
            int cellSize = atlas.width / cols;
            int count = cols * rows;

            var array = new Texture2DArray(cellSize, cellSize, count, TextureFormat.RGBA32, false, false);
            RenderTexture rt = RenderTexture.GetTemporary(cellSize, cellSize, 0, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(cellSize, cellSize, TextureFormat.RGBA32, false);
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                for (var i = 0; i < count; i++)
                {
                    int row = i / cols;
                    int col = i % cols;
                    var scale = new Vector2(1f / cols, 1f / rows);
                    var offset = new Vector2(col / (float)cols, (rows - 1 - row) / (float)rows);

                    Graphics.Blit(atlas, rt, scale, offset);
                    RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, cellSize, cellSize), 0, 0, false);
                    readback.Apply(false);
                    RenderTexture.active = previousActive;

                    Graphics.CopyTexture(readback, 0, 0, array, i, 0);
                }

                array.Apply(false, true);
            }
            finally
            {
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                GameObject.Destroy(readback);
            }

            return array;
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public VoiceChatConfigurationsReference VoiceChatConfigurations { get; private set; } = null!;

            [Serializable]
            public class VoiceChatConfigurationsReference : AssetReferenceT<VoiceChatPluginSettings>
            {
                public VoiceChatConfigurationsReference(string guid) : base(guid) { }
            }
        }
    }
}
