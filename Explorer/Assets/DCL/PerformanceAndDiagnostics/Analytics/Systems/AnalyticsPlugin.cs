using Arch.SystemGroups;
using DCL.Analytics.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Backpack.AvatarSection.Outfits.Analytics;
using DCL.DebugUtilities;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.Translation;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle;
using Utility;
using Utility.Json;
using ScreencaptureAnalyticsSystem = DCL.Analytics.Systems.ScreencaptureAnalyticsSystem;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin
    {
        private readonly IProfiler profiler;
        private readonly ILoadingStatus loadingStatus;
        private readonly IRealmData realmData;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IEventBus eventBus;
        private readonly IScenesCache scenesCache;
        private readonly ITranslationSettings translationSettings;

        private readonly WalkedDistanceAnalytics walkedDistanceAnalytics;
        private AutoTranslateAnalytics? autoTranslateAnalytics;
        private OutfitsAnalytics outfitsAnalytics;
        private readonly PlayerParcelChangedAnalytics playerParcelChangedAnalytics;

        public AnalyticsPlugin(
            IAnalyticsController analytics,
            IProfiler profiler,
            ILoadingStatus loadingStatus,
            IRealmData realmData,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IWeb3IdentityCache identityCache,
            IDebugContainerBuilder debugContainerBuilder,
            ICameraReelStorageService cameraReelStorageService,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IScenesCache scenesCache,
            IEventBus eventBus,
            ITranslationSettings translationSettings
        )
        {
            this.analytics = analytics;

            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
            this.realmData = realmData;
            this.identityCache = identityCache;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cameraReelStorageService = cameraReelStorageService;
            this.entityParticipantTable = entityParticipantTable;
            this.eventBus = eventBus;
            this.translationSettings = translationSettings;
            this.scenesCache = scenesCache;
            walkedDistanceAnalytics = new WalkedDistanceAnalytics(analytics, mainPlayerAvatarBaseProxy);
            playerParcelChangedAnalytics = new PlayerParcelChangedAnalytics(analytics, scenesCache);
        }

        public void Dispose()
        {
            walkedDistanceAnalytics.Dispose();
            playerParcelChangedAnalytics.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            walkedDistanceAnalytics.Initialize();

            autoTranslateAnalytics = new AutoTranslateAnalytics(analytics, eventBus, translationSettings);
            outfitsAnalytics = new OutfitsAnalytics(analytics, eventBus);

            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, loadingStatus, realmData, profiler, entityParticipantTable, new JsonObjectBuilder());
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
            MovementBadgesSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity, identityCache, debugContainerBuilder, walkedDistanceAnalytics);
            AnalyticsEmotesSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
            ScreencaptureAnalyticsSystem.InjectToWorld(ref builder, analytics, cameraReelStorageService);
            DebugAnalyticsSystem.InjectToWorld(ref builder, analytics, debugContainerBuilder);
        }
    }
}
