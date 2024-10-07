using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.PluginSystem.World
{
    public class MaterialsPlugin : IDCLWorldPlugin<MaterialsPlugin.Settings>
    {
        private readonly IPerformanceBudget capFrameTimeBudget;
        private readonly MemoryBudget memoryBudgetProvider;

        private readonly IExtendedObjectPool<Texture2D> videoTexturePool;

        private IObjectPool<Material> basicMatPool = null!;
        private IObjectPool<Material> pbrMatPool = null!;

        private DestroyMaterial destroyMaterial = null!;

        private int loadingAttemptsCount;

        public MaterialsPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IExtendedObjectPool<Texture2D> videoTexturePool)
        {
            memoryBudgetProvider = sharedDependencies.MemoryBudget;
            capFrameTimeBudget = sharedDependencies.FrameTimeBudget;
            this.videoTexturePool = videoTexturePool;
        }

        public void Dispose() { }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            basicMatPool = new ObjectPool<Material>(() => new Material(settings.basicMaterial), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.PoolInitialCapacity, maxSize: settings.PoolMaxSize);
            pbrMatPool = new ObjectPool<Material>(() => new Material(settings.pbrMaterial), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.PoolInitialCapacity, maxSize: settings.PoolMaxSize);

            destroyMaterial = (in MaterialData data, Material material) => { (data.IsPbrMaterial ? pbrMatPool : basicMatPool).Release(material); };

            loadingAttemptsCount = settings.LoadingAttemptsCount;
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            StartMaterialsLoadingSystem.InjectToWorld(ref builder, destroyMaterial, sharedDependencies.SceneData, loadingAttemptsCount, capFrameTimeBudget, sharedDependencies.EntitiesMap, videoTexturePool);

            // the idea with cache didn't work out: the CPU pressure is too high and benefits are not clear. Consider revising it when and if needed
            // LoadMaterialFromCacheSystem.InjectToWorld(ref builder, materialsCache);
            CreateBasicMaterialSystem.InjectToWorld(ref builder, basicMatPool, capFrameTimeBudget, memoryBudgetProvider);
            CreatePBRMaterialSystem.InjectToWorld(ref builder, pbrMatPool, capFrameTimeBudget, memoryBudgetProvider);
            ApplyMaterialSystem.InjectToWorld(ref builder, sharedDependencies.SceneData);
            ResetMaterialSystem.InjectToWorld(ref builder, destroyMaterial, sharedDependencies.SceneData);
            finalizeWorldSystems.Add(CleanUpMaterialsSystem.InjectToWorld(ref builder, destroyMaterial));
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            /// <summary>
            /// replaced from Addressables, Being in Addressables caused a problem with strobe-lights because shaders were not being compiled
            /// </summary>
            [field: SerializeField]
            public Material basicMaterial = null!;

            [field: SerializeField]
            public Material pbrMaterial = null!;
            [field: Header(nameof(MaterialsPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public int LoadingAttemptsCount { get; private set; } = 6;

            [field: SerializeField]
            public int PoolInitialCapacity { get; private set; } = 256;

            [field: SerializeField]
            public int PoolMaxSize { get; private set; } = 2048;
        }
    }
}
