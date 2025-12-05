using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioSources;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.AudioClips;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

namespace DCL.PluginSystem.World
{
    public class AudioAnalysisPlugin : IDCLWorldPlugin
    {
        private readonly FrameTimeCapBudget frameTimeBudgetProvider;
        private readonly MemoryBudget memoryBudgetProvider;

        public AudioAnalysisPlugin(ECSWorldSingletonSharedDependencies sharedDependencies)
        {
            frameTimeBudgetProvider = sharedDependencies.FrameTimeBudget;
            memoryBudgetProvider = sharedDependencies.MemoryBudget;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            AudioAnalysisSystem.InjectToWorld(ref builder, frameTimeBudgetProvider);

            finalizeWorldSystems.Add(CleanUpAudioAnalysisSystem.InjectToWorld(ref builder));
        }

        public void Dispose()
        {
        }

        public async UniTask InitializeAsync(CancellationToken ct)
        {
        }
    }
}

