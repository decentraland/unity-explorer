using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Audio;
using DCL.ChatArea;
using DCL.Communities.CommunitiesDataProvider;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Friends.UserBlocking;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Tables;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.UI.Profiles.Helpers;
using ECS.SceneLifeCycle;
using DCL.VoiceChat;
using DCL.VoiceChat.CommunityVoiceChat;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.Systems;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms;
using System;
using System.Collections.Concurrent;
using System.Threading;
using DCL.UI;
using DCL.Utilities;
using DCL.Utilities.Extensions;
using UnityEngine;
using UnityEngine.AddressableAssets;
using DCL.FeatureFlags;
using DCL.Prefs;
using DCL.RealmNavigation;
using DCL.VoiceChat.UI;
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
        private readonly ConcurrentDictionary<string, LivekitAudioSource> nearbyAudioSources = new ();
        private readonly NearbyMuteService? nearbyMuteService;
        private readonly ObjectProxy<IUserBlockingCache> blockingCacheProxy;
        private readonly NearbyVoiceChatButtonView nearbyVoiceChatButtonView;
        private readonly NearbyVoiceWidgetView nearbyVoiceWidgetView;
        private readonly NearbyVoiceTipView nearbyVoiceTipView;
        private readonly ILoadingStatus loadingStatus;
        private readonly IScenesCache scenesCache;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly VolumeBus volumeBus;

        private ProvidedAsset<VoiceChatPluginSettings> voiceChatPluginSettingsAsset;
        private VoiceChatMicrophoneHandler? voiceChatHandler;
        private MicrophoneTrackPublisher? microphonePublisher;
        private RemoteTrackListener? remoteListener;
        private VoiceChatRoomManager? roomManager;
        private VoiceChatNametagsHandler? nametagsHandler;
        private VoiceChatMicrophoneStateManager? microphoneStateManager;
        private MicrophoneAudioToggleHandler? microphoneAudioToggleHandler;
        private VoiceChatPanelPresenter? voiceChatPanelPresenter;
        private VoiceChatDebugContainer? voiceChatDebugContainer;
        private NearbyVoiceChatManager? nearbyVoiceChatManager;
        private NearbyVoiceChatStateModel? nearbyStateModel;
        private NearbyVoiceChatButtonController? nearbyButtonController;
        private NearbyVoiceWidgetController? nearbyWidgetController;
        private CancellationTokenSource? nearbyTipCts;
        private VoiceChatConfiguration voiceChatConfiguration;

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
            ILoadingStatus loadingStatus,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionBusController,
            NearbyVoiceChatButtonView nearbyVoiceChatButtonView,
            NearbyVoiceWidgetView nearbyVoiceWidgetView,
            NearbyVoiceTipView nearbyVoiceTipView,
            VolumeBus volumeBus,
            ObjectProxy<IUserBlockingCache> blockingCacheProxy,
            NearbyMuteService? nearbyMuteService = null)
        {
            this.nearbyMuteService = nearbyMuteService;
            this.blockingCacheProxy = blockingCacheProxy;
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
            this.loadingStatus = loadingStatus;
            this.scenesCache = scenesCache;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.nearbyVoiceChatButtonView = nearbyVoiceChatButtonView;
            this.nearbyVoiceWidgetView = nearbyVoiceWidgetView;
            this.nearbyVoiceTipView = nearbyVoiceTipView;
            this.volumeBus = volumeBus;

            voiceChatOrchestrator = voiceChatContainer.VoiceChatOrchestrator;
        }

        public void Dispose()
        {
            nearbyTipCts.SafeCancelAndDispose();
            pluginScope.Dispose();

            if (voiceChatPluginSettingsAsset.Value != null)
                voiceChatPluginSettingsAsset.Dispose();

            RustAudioClient.DeInit();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                NearbyAudioPositionSystem.InjectToWorld(ref builder, entityParticipantTable, nearbyAudioSources);
                NearbyAudioDebugSystem.InjectToWorld(ref builder, voiceChatConfiguration, debugContainer);
            }
        }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            ReportHub.LogWarning(ReportCategory.VOICE_CHAT, "VOICE CHAT!");
            AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
            audioConfig.sampleRate = VoiceChatConstants.LIVEKIT_SAMPLE_RATE;
            AudioSettings.Reset(audioConfig);

            voiceChatPluginSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.VoiceChatConfigurations, ct: ct);

            VoiceChatPluginSettings pluginSettings = voiceChatPluginSettingsAsset.Value;
            voiceChatConfiguration = pluginSettings.VoiceChatConfiguration;

            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration, voiceChatOrchestrator);
            pluginScope.Add(voiceChatHandler);

            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);
            pluginScope.Add(microphoneStateManager);

            microphonePublisher = new MicrophoneTrackPublisher(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, VoiceChatType.COMMUNITY);
            remoteListener = new RemoteTrackListener(
                roomHub.VoiceChatRoom().Room(),
                voiceChatConfiguration,
                new PlaybackSourcesHub("Call", voiceChatConfiguration.ChatAudioMixerGroup.EnsureNotNull(), false));

            roomManager = new VoiceChatRoomManager(microphonePublisher, remoteListener, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager, voiceChatHandler);
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

            voiceChatDebugContainer = new VoiceChatDebugContainer(debugContainer, microphonePublisher, remoteListener);
            pluginScope.Add(voiceChatDebugContainer);

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                IRoom islandRoom = roomHub.IslandRoom();

                NearbyVoiceChatState initialState = DCLPlayerPrefs.GetBool(DCLPrefKeys.NEARBY_VOICE_CHAT_DISABLED)
                    ? NearbyVoiceChatState.DISABLED
                    : NearbyVoiceChatState.IDLE;

                nearbyStateModel = new NearbyVoiceChatStateModel(initialState);
                pluginScope.Add(nearbyStateModel);

                var sceneRestrictionWatcher = new NearbyVoiceSceneRestrictionWatcher(scenesCache, sceneRestrictionBusController, nearbyStateModel);
                pluginScope.Add(sceneRestrictionWatcher);

                await nearbyMuteService!.LoadAsync(ct);

                nearbyVoiceChatManager = new NearbyVoiceChatManager(
                    islandRoom, voiceChatConfiguration,
                    nearbyAudioSources, voiceChatOrchestrator.CurrentCallStatus,
                    nearbyStateModel, nearbyMuteService, blockingCacheProxy, loadingStatus);
                pluginScope.Add(nearbyVoiceChatManager);

                nearbyButtonController = new NearbyVoiceChatButtonController(nearbyVoiceChatButtonView, nearbyStateModel);
                pluginScope.Add(nearbyButtonController);

                nearbyWidgetController = new NearbyVoiceWidgetController(nearbyVoiceWidgetView, nearbyStateModel, voiceChatConfiguration.ChatAudioMixerGroup, volumeBus);
                pluginScope.Add(nearbyWidgetController);

                nearbyTipCts = new CancellationTokenSource();
                RunNearbyVoiceTipAsync(nearbyVoiceTipView, loadingStatus, nearbyVoiceChatButtonView, nearbyTipCts.Token).Forget();
            }
        }

        private static async UniTaskVoid RunNearbyVoiceTipAsync(NearbyVoiceTipView view, ILoadingStatus loadingStatus,
            NearbyVoiceChatButtonView buttonView, CancellationToken ct)
        {
            if (await NearbyVoiceTipFlow.WaitAndShowAsync(view, loadingStatus, ct))
                buttonView.Button.onClick.Invoke();
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

        private static class NearbyVoiceTipFlow
        {
            public static async UniTask<bool> WaitAndShowAsync(NearbyVoiceTipView view, ILoadingStatus loadingStatus, CancellationToken ct)
            {
                view.Hide();

                if (DCLPlayerPrefs.GetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED))
                    return false;

                try
                {
                    await UniTask.WaitUntil(
                        () => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed,
                        cancellationToken: ct);

                    view.Show();

                    int winner = await UniTask.WhenAny(
                        view.CloseButton.OnClickAsync(ct),
                        view.TryItNowButton.OnClickAsync(ct));

                    DCLPlayerPrefs.SetBool(DCLPrefKeys.NEARBY_VOICE_TIP_DISMISSED, true, save: true);
                    view.Hide();

                    return winner == 1;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }
    }
}
