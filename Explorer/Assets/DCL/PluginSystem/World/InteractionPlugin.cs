﻿using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Raycast.Systems;
using DCL.Interaction.Settings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiling;
using DCL.Utilities.Extensions;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using InteractionHighlightSystem = DCL.Interaction.Systems.InteractionHighlightSystem;
using RaycastHit = DCL.ECSComponents.RaycastHit;

namespace DCL.PluginSystem.World
{
    public class InteractionPlugin : IDCLWorldPlugin<InteractionPlugin.Settings>
    {
        private readonly IGlobalInputEvents globalInputEvents;
        private readonly IBudgetProfiler profiler;
        private readonly ECSWorldSingletonSharedDependencies sharedDependencies;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IAssetsProvisioner assetsProvisioner;

        private IReleasablePerformanceBudget raycastBudget = null!;
        private Settings settings = null!;
        private InteractionSettingsData interactionData = null!;

        public InteractionPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IBudgetProfiler profiler, IGlobalInputEvents globalInputEvents, IComponentPoolsRegistry poolsRegistry, IAssetsProvisioner assetsProvisioner)
        {
            this.sharedDependencies = sharedDependencies;
            this.profiler = profiler;
            this.globalInputEvents = globalInputEvents;
            this.poolsRegistry = poolsRegistry;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void Dispose() { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            this.settings = settings;
            raycastBudget = new FrameTimeSharedBudget(settings.RaycastFrameBudgetMs, profiler);
            interactionData = (await assetsProvisioner.ProvideMainAssetAsync(this.settings.Data, ct)).Value;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sceneDeps,
            in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            IComponentPool<PBRaycastResult> raycastResultPool = sharedDependencies
                                                               .ComponentPoolsRegistry
                                                               .GetReferenceTypePool<PBRaycastResult>()
                                                               .EnsureNotNull();

            InitializeRaycastSystem.InjectToWorld(ref builder);

            sceneIsCurrentListeners.Add(
                ExecuteRaycastSystem.InjectToWorld(
                    ref builder,
                    persistentEntities.SceneRoot,
                    raycastBudget,
                    settings.RaycastBucketThreshold,
                    sharedDependencies.ComponentPoolsRegistry.GetReferenceTypePool<RaycastHit>(),
                    raycastResultPool,
                    sceneDeps.EntityCollidersSceneCache,
                    sceneDeps.EntitiesMap,
                    sceneDeps.EcsToCRDTWriter,
                    sceneDeps.SceneStateProvider
                )
            );

            WritePointerEventResultsSystem.InjectToWorld(
                ref builder,
                persistentEntities.SceneRoot,
                sceneDeps.EcsToCRDTWriter,
                sceneDeps.SceneStateProvider,
                globalInputEvents,
                poolsRegistry.GetReferenceTypePool<RaycastHit>()
            );

            sceneIsCurrentListeners.Add(InteractionHighlightSystem.InjectToWorld(ref builder, interactionData,
                sceneDeps.SceneStateProvider));
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] internal AssetReferenceT<InteractionSettingsData> Data;
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
        }
    }
}
