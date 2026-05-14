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
using DCL.VoiceChat.Nearby.Audio;
using DCL.VoiceChat.Nearby.Systems;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using LiveKit.Rooms;
using Global.AppArgs;
using System;
using System.Collections.Generic;
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
using Utility.Multithreading;
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
        private readonly NearbyMuteService? nearbyMuteService;
        private readonly NearbyVoiceChatStateModel? nearbyStateModel;
        private readonly IUserBlockingCache userBlockingCache;
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
        private NearbyAudioStreamsRegistry? nearbyAudioStreamRegistry;
        private HashSet<StreamKey>? nearbyAudioBindings;
        private NearbyAudioSourceFactory? nearbyAudioSourceFactory;
        private NearbyVoiceChatManager? nearbyVoiceChatManager;
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
            IUserBlockingCache userBlockingCache,
            NearbyMuteService? nearbyMuteService = null,
            NearbyVoiceChatStateModel? nearbyStateModel = null)
        {
            this.nearbyMuteService = nearbyMuteService;
            this.nearbyStateModel = nearbyStateModel;
            this.userBlockingCache = userBlockingCache;
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
            // EXIT-DELAY BISECTION (#8764): when --exit-test-disconnect-rooms-on-quit is set,
            // disconnect the LiveKit rooms explicitly before tearing down the plugin. Rationale:
            // livekit_ffi tokio workers get attached to the IL2CPP managed runtime via the first
            // managed callback they fire (track subscribed, participant update, etc.) and never
            // detach. The only way to make them detach is to make the threads themselves exit,
            // which happens when the tokio runtime owning them shuts down — which in turn happens
            // when the LiveKit Room is disconnected. We block up to 3s for completion so we cap
            // any potential hang in the disconnect path itself.
            if (HasExitTestDisconnectRoomsOnQuit())
            {
                ReportHub.LogWarning(ReportCategory.ALWAYS, "EXIT TEST: disconnecting LiveKit rooms on quit");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    roomHub.IslandRoom().DisconnectAsync(cts.Token).AsTask().Wait(cts.Token);
                }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.ALWAYS, $"EXIT TEST: IslandRoom disconnect failed: {ex.Message}"); }
                try
                {
                    roomHub.VoiceChatRoom().Room().DisconnectAsync(cts.Token).AsTask().Wait(cts.Token);
                }
                catch (Exception ex) { ReportHub.LogWarning(ReportCategory.ALWAYS, $"EXIT TEST: VoiceChatRoom disconnect failed: {ex.Message}"); }
            }

            nearbyTipCts.SafeCancelAndDispose();
            pluginScope.Dispose();

            if (voiceChatPluginSettingsAsset.Value != null)
                voiceChatPluginSettingsAsset.Dispose();

            RustAudioClient.DeInit();
        }

        // EXIT-DELAY BISECTION (#8764): helpers to read --exit-test-* flags directly
        // from the process command line. Kept local to avoid plumbing IAppArgs through
        // the plugin's DI graph for an investigation toggle.
        private static int GetExitTestVoiceInitStopStage()
        {
            string[] args = Environment.GetCommandLineArgs();
            string prefix = "--" + AppArgsFlags.EXIT_TEST_VOICE_INIT_STOP + "=";
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(args[i].AsSpan(prefix.Length), out int n))
                    return n;
            }
            return 0; // 0 = no stop, full init
        }

        private static bool HasExitTestSkipNearbyVoiceSystems()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dashed = "--" + AppArgsFlags.EXIT_TEST_SKIP_NEARBY_VOICE_SYSTEMS;
            for (var i = 0; i < args.Length; i++)
                if (args[i] == dashed)
                    return true;
            return false;
        }

        private static int GetExitTestNearbyInjectStopStage()
        {
            string[] args = Environment.GetCommandLineArgs();
            string prefix = "--" + AppArgsFlags.EXIT_TEST_NEARBY_INJECT_STOP + "=";
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith(prefix, StringComparison.Ordinal)
                    && int.TryParse(args[i].AsSpan(prefix.Length), out int n))
                    return n;
            }
            return 0; // 0 = no stop, all systems injected
        }

        private static bool HasExitTestDisconnectRoomsOnQuit()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dashed = "--" + AppArgsFlags.EXIT_TEST_DISCONNECT_ROOMS_ON_QUIT;
            for (var i = 0; i < args.Length; i++)
                if (args[i] == dashed)
                    return true;
            return false;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // EXIT-DELAY BISECTION (#8764): skip the nearby voice ECS systems independently
            // of the InitializeAsync stop, and defensively guard against null when init was
            // halted before the NEARBY block ran.
            if (HasExitTestSkipNearbyVoiceSystems())
            {
                ReportHub.LogWarning(ReportCategory.ALWAYS, "EXIT TEST: skipping NEARBY voice systems in InjectToWorld");
                return;
            }
            if (nearbyAudioStreamRegistry == null)
                return;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                // EXIT-DELAY BISECTION (#8764):
                // Read --exit-test-nearby-inject-stop=N and stop after registering N ECS systems
                // to identify which NEARBY voice system is leaving livekit_ffi tokio threads
                // attached to the IL2CPP runtime.
                int stopAfter = GetExitTestNearbyInjectStopStage();
                if (stopAfter > 0)
                    ReportHub.LogWarning(ReportCategory.ALWAYS, $"EXIT TEST: NEARBY inject will stop after stage {stopAfter}");

                var listenerState = new NearbyListenerState();

                // Stage 1: NearbyLivekitBridgeSystem (primary suspect, bridges to livekit_ffi)
                NearbyLivekitBridgeSystem.InjectToWorld(ref builder, nearbyAudioStreamRegistry!);
                if (stopAfter == 1) return;

                // Stage 2: NearbyAudibleRangeSystem
                NearbyAudibleRangeSystem.InjectToWorld(ref builder, voiceChatConfiguration, listenerState);
                if (stopAfter == 2) return;

                // Stage 3: NearbyAudioBindingSystem
                NearbyAudioBindingSystem.InjectToWorld(ref builder, nearbyAudioStreamRegistry!, nearbyAudioBindings!, userBlockingCache, nearbyStateModel!, nearbyAudioSourceFactory!);
                if (stopAfter == 3) return;

                // Stage 4: NearbyAudioPositionSystem
                NearbyAudioPositionSystem.InjectToWorld(ref builder, nearbyMuteService!, listenerState);
                if (stopAfter == 4) return;

                // Stage 5: NearbyAudioCleanupSystem
                NearbyAudioCleanupSystem.InjectToWorld(ref builder, nearbyAudioStreamRegistry!, nearbyAudioBindings!, userBlockingCache, nearbyStateModel!, nearbyAudioSourceFactory!);
                if (stopAfter == 5) return;

                // Stage 6: NearbyVoiceChatNametagSystem
                NearbyVoiceChatNametagSystem.InjectToWorld(ref builder, playerEntity, nearbyAudioStreamRegistry!, nearbyStateModel!, nearbyMuteService!);
                if (stopAfter == 6) return;

                // Stage 7: NearbyVoiceChatDebugSystem
                NearbyVoiceChatDebugSystem.InjectToWorld(ref builder, voiceChatConfiguration, debugContainer, roomHub.IslandRoom(), nearbyStateModel!, nearbyAudioStreamRegistry!, entityParticipantTable);
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

            // EXIT-DELAY BISECTION (#8764):
            // Read --exit-test-voice-init-stop=N and stop initialization after stage N to
            // identify which voice chat component keeps livekit_ffi tokio threads attached
            // to the IL2CPP runtime, preventing process shutdown.
            int stopAfter = GetExitTestVoiceInitStopStage();
            if (stopAfter > 0)
                ReportHub.LogWarning(ReportCategory.ALWAYS, $"EXIT TEST: VoiceChat init will stop after stage {stopAfter}");

            // Stage 1: local-only handlers (no Room access)
            voiceChatHandler = new VoiceChatMicrophoneHandler(voiceChatConfiguration, voiceChatOrchestrator);
            pluginScope.Add(voiceChatHandler);

            microphoneStateManager = new VoiceChatMicrophoneStateManager(voiceChatHandler, voiceChatOrchestrator);
            pluginScope.Add(microphoneStateManager);
            if (stopAfter == 1) return;

            // Stage 2: MicrophoneTrackPublisher — first component that touches voice chat Room
            microphonePublisher = new MicrophoneTrackPublisher(roomHub.VoiceChatRoom().Room(), voiceChatConfiguration, VoiceChatType.COMMUNITY);
            if (stopAfter == 2) return;

            // Stage 3: RemoteTrackListener — second component that touches voice chat Room
            var callPlaybackSourcesHub = new PlaybackSourcesHub("Call", voiceChatConfiguration.ChatAudioMixerGroup.EnsureNotNull());
            remoteListener = new RemoteTrackListener(
                roomHub.VoiceChatRoom().Room(),
                voiceChatConfiguration,
                callPlaybackSourcesHub);
            if (stopAfter == 3) return;

            // Stage 4: VoiceChatRoomManager (orchestrates publisher/listener)
            roomManager = new VoiceChatRoomManager(microphonePublisher, remoteListener, roomHub, roomHub.VoiceChatRoom().Room(), voiceChatOrchestrator, voiceChatConfiguration, microphoneStateManager, voiceChatHandler);
            pluginScope.Add(roomManager);
            if (stopAfter == 4) return;

            // Stage 5: VoiceChatNametagsHandler (subscribes to Room events)
            nametagsHandler = new VoiceChatNametagsHandler(
                roomHub.VoiceChatRoom().Room(),
                voiceChatOrchestrator,
                entityParticipantTable,
                world,
                playerEntity);
            pluginScope.Add(nametagsHandler);
            if (stopAfter == 5) return;

            // Stage 6: MicrophoneAudioToggleHandler (audio cues, no Room access)
            VoiceChatParticipantEntryView playerEntry = pluginSettings.PlayerEntryView;
            AudioClipConfig muteMicrophoneAudio = pluginSettings.MuteMicrophoneAudio;
            AudioClipConfig unmuteMicrophoneAudio = pluginSettings.UnmuteMicrophoneAudio;
            microphoneAudioToggleHandler = new MicrophoneAudioToggleHandler(voiceChatHandler, muteMicrophoneAudio, unmuteMicrophoneAudio);
            pluginScope.Add(microphoneAudioToggleHandler);
            if (stopAfter == 6) return;

            // Stage 7: VoiceChatPanelPresenter (UI)
            voiceChatPanelPresenter = new VoiceChatPanelPresenter(voiceChatPanelView, profileDataProvider, communityDataProvider, imageControllerProvider, voiceChatOrchestrator, voiceChatHandler, roomManager, roomHub, playerEntry, chatSharedAreaEventBus);
            pluginScope.Add(voiceChatPanelPresenter);
            if (stopAfter == 7) return;

            // Stage 8: VoiceChatDebugContainer
            voiceChatDebugContainer = new VoiceChatDebugContainer(debugContainer, microphonePublisher, roomHub.VoiceChatRoom().Room(), callPlaybackSourcesHub);
            pluginScope.Add(voiceChatDebugContainer);
            if (stopAfter == 8) return;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.NEARBY_VOICE_CHAT))
            {
                IRoom islandRoom = roomHub.IslandRoom();

                nearbyAudioStreamRegistry = new NearbyAudioStreamsRegistry(islandRoom);
                pluginScope.Add(nearbyAudioStreamRegistry);

                nearbyAudioBindings = new HashSet<StreamKey>(32);
                nearbyAudioSourceFactory = new NearbyAudioSourceFactory(voiceChatConfiguration);

                // State model is created in DynamicWorldContainer so analytics can subscribe to it.
                NearbyVoiceChatStateModel stateModel = nearbyStateModel!;
                pluginScope.Add(stateModel);

                // Persist the user's on/off preference of the nearby chat.
                stateModel.State.Subscribe(newState =>
                {
                    if (newState is NearbyVoiceChatState.DISABLED or NearbyVoiceChatState.IDLE)
                        DCLPlayerPrefs.SetBool(DCLPrefKeys.NEARBY_VOICE_CHAT_DISABLED, newState == NearbyVoiceChatState.DISABLED);
                });

                var sceneRestrictionWatcher = new NearbyVoiceSceneRestrictionWatcher(scenesCache, sceneRestrictionBusController, stateModel);
                pluginScope.Add(sceneRestrictionWatcher);

                nearbyMuteService!.LoadAsync(ct).Forget();

                nearbyVoiceChatManager = new NearbyVoiceChatManager(stateModel, islandRoom, voiceChatConfiguration, voiceChatOrchestrator.CurrentCallStatus, loadingStatus);
                pluginScope.Add(nearbyVoiceChatManager);

                // UI
                nearbyButtonController = new NearbyVoiceChatButtonController(nearbyVoiceChatButtonView, stateModel);
                pluginScope.Add(nearbyButtonController);

                nearbyWidgetController = new NearbyVoiceWidgetController(nearbyVoiceWidgetView, stateModel, voiceChatConfiguration.ChatAudioMixerGroup, volumeBus);
                pluginScope.Add(nearbyWidgetController);

                // Intro FLUX
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
