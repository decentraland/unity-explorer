﻿using Arch.SystemGroups;
using DCL.Analytics.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.DebugUtilities;
using DCL.InWorldCamera.CameraReelStorageService;
using DCL.Multiplayer.Profiles.Tables;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PerformanceAndDiagnostics.Analytics.EventBased;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.Utilities;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle;
using DCL.Translation.Settings;
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
        private readonly IScenesCache scenesCache;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IAnalyticsController analytics;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ICameraReelStorageService cameraReelStorageService;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly IEventBus eventBus;
        private readonly ITranslationSettings translationSettings;

        private readonly WalkedDistanceAnalytics walkedDistanceAnalytics;
        private AutoTranslateAnalytics? autoTranslateAnalytics;

        public AnalyticsPlugin(
            IAnalyticsController analytics,
            IProfiler profiler,
            ILoadingStatus loadingStatus,
            IRealmData realmData,
            IScenesCache scenesCache,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IWeb3IdentityCache identityCache,
            IDebugContainerBuilder debugContainerBuilder,
            ICameraReelStorageService cameraReelStorageService,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            IEventBus eventBus,
            ITranslationSettings translationSettings
        )
        {
            this.analytics = analytics;

            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
            this.identityCache = identityCache;
            this.debugContainerBuilder = debugContainerBuilder;
            this.cameraReelStorageService = cameraReelStorageService;
            this.entityParticipantTable = entityParticipantTable;
            this.eventBus = eventBus;
            this.translationSettings = translationSettings;

            walkedDistanceAnalytics = new WalkedDistanceAnalytics(analytics, mainPlayerAvatarBaseProxy);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            walkedDistanceAnalytics.Initialize();

            autoTranslateAnalytics = new AutoTranslateAnalytics(analytics, eventBus, translationSettings);

            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, loadingStatus, realmData, profiler, entityParticipantTable, new JsonObjectBuilder());
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
            MovementBadgesSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity, identityCache, debugContainerBuilder, walkedDistanceAnalytics);
            AnalyticsEmotesSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
            ScreencaptureAnalyticsSystem.InjectToWorld(ref builder, analytics, cameraReelStorageService);
            DebugAnalyticsSystem.InjectToWorld(ref builder, analytics, debugContainerBuilder);
        }

        public void Dispose()
        {
            walkedDistanceAnalytics.Dispose();
            autoTranslateAnalytics?.Dispose();
        }
    }
}
