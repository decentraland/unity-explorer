using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
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
        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: Header(nameof(MaterialsPlugin) + "." + nameof(Settings))]
            [field: Space]
            [field: SerializeField]
            public int LoadingAttemptsCount { get; private set; } = 6;

            [field: SerializeField]
            public int PoolInitialCapacity { get; private set; } = 256;

            [field: SerializeField]
            public int PoolMaxSize { get; private set; } = 2048;

            [field: SerializeField]
            public AssetReferenceMaterial basicMaterial;

            [field: SerializeField]
            public AssetReferenceMaterial pbrMaterial;
        }

        // private const int CACHE_CAPACITY = 512;
        // private readonly IMaterialsCache materialsCache;

        private readonly IConcurrentBudgetProvider capFrameTimeBudgetProvider;
        private readonly IAssetsProvisioner assetsProvisioner;

        private IObjectPool<Material> basicMatPool;
        private IObjectPool<Material> pbrMatPool;

        private DestroyMaterial destroyMaterial;

        private int loadingAttemptsCount;

        public MaterialsPlugin(ECSWorldSingletonSharedDependencies sharedDependencies, IAssetsProvisioner assetsProvisioner)
        {
            capFrameTimeBudgetProvider = sharedDependencies.FrameTimeBudgetProvider;
            this.assetsProvisioner = assetsProvisioner;

            // materialsCache = new MaterialsCappedCache(CACHE_CAPACITY, (in MaterialData data, Material material) => { (data.IsPbrMaterial ? pbrMatPool : basicMatPool).Release(material); });
        }

        public async UniTask Initialize(Settings settings, CancellationToken ct)
        {
            ProvidedAsset<Material> basicMatReference = await assetsProvisioner.ProvideMainAsset(settings.basicMaterial, ct: ct);
            ProvidedAsset<Material> pbrMaterialReference = await assetsProvisioner.ProvideMainAsset(settings.pbrMaterial, ct: ct);

            basicMatPool = new ObjectPool<Material>(() => new Material(basicMatReference.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.PoolInitialCapacity, maxSize: settings.PoolMaxSize);
            pbrMatPool = new ObjectPool<Material>(() => new Material(pbrMaterialReference.Value), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: settings.PoolInitialCapacity, maxSize: settings.PoolMaxSize);

            destroyMaterial = (in MaterialData data, Material material) => { (data.IsPbrMaterial ? pbrMatPool : basicMatPool).Release(material); };

            loadingAttemptsCount = settings.LoadingAttemptsCount;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartMaterialsLoadingSystem.InjectToWorld(ref builder, destroyMaterial, sharedDependencies.SceneData, loadingAttemptsCount, capFrameTimeBudgetProvider);

            // the idea with cache didn't work out: the CPU pressure is too high and benefits are not clear
            // consider revising when and if needed
            // LoadMaterialFromCacheSystem.InjectToWorld(ref builder, materialsCache);
            CreateBasicMaterialSystem.InjectToWorld(ref builder, basicMatPool, capFrameTimeBudgetProvider);
            CreatePBRMaterialSystem.InjectToWorld(ref builder, pbrMatPool, capFrameTimeBudgetProvider);
            ApplyMaterialSystem.InjectToWorld(ref builder);
            ResetMaterialSystem.InjectToWorld(ref builder, destroyMaterial);
            CleanUpMaterialsSystem.InjectToWorld(ref builder, destroyMaterial);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }

        public void Dispose() { }
    }
}
