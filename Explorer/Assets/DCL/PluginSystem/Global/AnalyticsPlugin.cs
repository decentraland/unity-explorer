using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiling;
using ECS;
using ECS.SceneLifeCycle;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin
    {
        private readonly IProfiler profiler;
        private readonly IRealmData realmData;
        private readonly IScenesCache scenesCache;

        private readonly IAnalyticsController analytics;

        public AnalyticsPlugin(IAnalyticsController analytics, IProfiler profiler, IRealmData realmData, IScenesCache scenesCache)
        {
            this.analytics = analytics;

            this.profiler = profiler;
            this.realmData = realmData;
            this.scenesCache = scenesCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, scenesCache, arguments.PlayerEntity);
            WalkedDistanceAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
            PerformanceAnalyticsSystem.InjectToWorld(ref builder, analytics, profiler);
            TimeSpentInWorldAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData);
        }

        public void Dispose() { }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
