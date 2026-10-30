using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AudioSources;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;

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

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            AudioAnalysisSystem.InjectToWorld(ref builder, frameTimeBudgetProvider, sharedDependencies.EcsToCRDTWriter);

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

