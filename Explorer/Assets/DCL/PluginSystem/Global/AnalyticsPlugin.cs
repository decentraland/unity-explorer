using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using DCL.Utilities;
using ECS;
using ECS.SceneLifeCycle;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin
    {
        private readonly IAnalyticsReportProfiler profiler;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;
        private readonly IAnalyticsController analytics;

        private readonly WalkedDistanceAnalytics walkedDistanceAnalytics;

        public AnalyticsPlugin(IAnalyticsController analytics, IAnalyticsReportProfiler profiler, IRealmData realmData, IScenesCache scenesCache, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy)
        {
            this.analytics = analytics;

            this.profiler = profiler;
            this.realmData = realmData;
            this.scenesCache = scenesCache;

            walkedDistanceAnalytics = new WalkedDistanceAnalytics(analytics, mainPlayerAvatarBaseProxy);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            walkedDistanceAnalytics.Initialize();

            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, profiler);
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
            BadgesHeightReachedSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
        }



        public void Dispose()
        {
            walkedDistanceAnalytics.Dispose();
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
