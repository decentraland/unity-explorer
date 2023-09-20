using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.Profiling;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.PluginSystem.World
{
    public class InteractionPlugin : IDCLWorldPlugin<InteractionPlugin.Settings>
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;

        private IConcurrentBudgetProvider raycastBudgetProvider;
        private Settings settings;

        public InteractionPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IProfilingProvider profilingProvider)
        {
            this.sharedDependencies = sharedDependencies;
            this.profilingProvider = profilingProvider;
        }

        public UniTask Initialize(Settings settings, CancellationToken ct)
        {
            this.settings = settings;
            raycastBudgetProvider = new FrameTimeSharedBudgetProvider(settings.RaycastFrameBudgetMs, profilingProvider);
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sceneDeps,
            in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            InitializeRaycastSystem.InjectToWorld(ref builder);

            ExecuteRaycastSystem.InjectToWorld(ref builder, persistentEntities.SceneRoot, raycastBudgetProvider, settings.RaycastBucketThreshold,
                sharedDependencies.ComponentPoolsRegistry.GetReferenceTypePool<RaycastHit>(),
                sharedDependencies.ComponentPoolsRegistry.GetReferenceTypePool<PBRaycastResult>(),
                sceneDeps.EntityCollidersSceneCache,
                sceneDeps.EntitiesMap,
                sceneDeps.EcsToCRDTWriter,
                sceneDeps.SceneStateProvider);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        public void Dispose() { }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(InteractionPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public byte RaycastBucketThreshold { get; set; } = 3;

            [field: SerializeField]
            public float RaycastFrameBudgetMs { get; set; } = 3f;
        }
    }
}
