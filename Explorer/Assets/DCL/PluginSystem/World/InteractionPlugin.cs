using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Raycast.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using InteractionHighlightSystem = DCL.Interaction.Systems.InteractionHighlightSystem;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.PluginSystem.World
{
    public class InteractionPlugin : IDCLWorldPlugin<InteractionPlugin.Settings>
    {
        private readonly IGlobalInputEvents globalInputEvents;
        private readonly IProfilingProvider profilingProvider;
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;

        private IReleasablePerformanceBudget raycastBudget;
        private Settings settings;
        private Material hoverMaterial;
        private Material hoverOorMaterial;

        public InteractionPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IProfilingProvider profilingProvider, IGlobalInputEvents globalInputEvents, IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner)
        {
            this.sharedDependencies = sharedDependencies;
            this.profilingProvider = profilingProvider;
            this.globalInputEvents = globalInputEvents;
            this.poolsRegistry = poolsRegistry;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            this.settings = settings;
            raycastBudget = new FrameTimeSharedBudget(settings.RaycastFrameBudgetMs, profilingProvider);
            hoverMaterial = (await assetsProvisioner.ProvideMainAssetAsync(this.settings.hoverMaterial, ct)).Value;
            hoverOorMaterial = (await assetsProvisioner.ProvideMainAssetAsync(this.settings.hoverOutOfRangeMaterial, ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sceneDeps,
            in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InitializeRaycastSystem.InjectToWorld(ref builder);

            ExecuteRaycastSystem.InjectToWorld(ref builder, sceneDeps.SceneData, raycastBudget, settings.RaycastBucketThreshold,
                sharedDependencies.ComponentPoolsRegistry.GetReferenceTypePool<RaycastHit>(),
                sharedDependencies.ComponentPoolsRegistry.GetReferenceTypePool<PBRaycastResult>(),
                sceneDeps.EntityCollidersSceneCache,
                sceneDeps.EntitiesMap,
                sceneDeps.EcsToCRDTWriter,
                sceneDeps.SceneStateProvider);

            WritePointerEventResultsSystem.InjectToWorld(ref builder, sceneDeps.SceneData,
                sceneDeps.EcsToCRDTWriter,
                sceneDeps.SceneStateProvider,
                globalInputEvents,
                poolsRegistry.GetReferenceTypePool<RaycastHit>());

            InteractionHighlightSystem.InjectToWorld(ref builder, hoverMaterial, hoverOorMaterial);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(InteractionPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public byte RaycastBucketThreshold { get; private set; } = 3;

            [field: SerializeField]
            public float RaycastFrameBudgetMs { get; private set; } = 3f;

            [field: SerializeField]
            public float PlayerOriginRaycastMaxDistance { get; private set; } = 200f;

            /// <summary>
            ///     Maximum scene bucket to which global input will be propagated.
            /// </summary>
            [field: SerializeField]
            public int GlobalInputPropagationBucketThreshold { get; private set; } = 3;

            [field: SerializeField] internal AssetReferenceMaterial hoverMaterial { get; private set; }
            [field: SerializeField] internal AssetReferenceMaterial hoverOutOfRangeMaterial { get; private set; }
        }
    }
}
