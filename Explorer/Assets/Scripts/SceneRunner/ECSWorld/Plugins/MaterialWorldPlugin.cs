using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace SceneRunner.ECSWorld.Plugins
{
    public class MaterialsPlugin : IECSWorldPlugin
    {
        // private const int CACHE_CAPACITY = 512;
        private const int LOADING_ATTEMPTS_COUNT = 6;

        private const int POOL_INITIAL_CAPACITY = 256;
        private const int POOL_MAX_SIZE = 2048;

        // private readonly IMaterialsCache materialsCache;

        private readonly IObjectPool<Material> basicMatPool;
        private readonly IObjectPool<Material> pbrMatPool;

        private readonly DestroyMaterial destroyMaterial;
        private readonly IConcurrentBudgetProvider instantiationFrameBudgetProvider;

        public MaterialsPlugin(ECSWorldSingletonSharedDependencies singletonSharedDependencies)
        {
            Material basicMatReference = Resources.Load<Material>(CreateBasicMaterialSystem.MATERIAL_PATH);
            Material pbrMaterialReference = Resources.Load<Material>(CreatePBRMaterialSystem.MATERIAL_PATH);

            basicMatPool = new ObjectPool<Material>(() => new Material(basicMatReference), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: POOL_INITIAL_CAPACITY, maxSize: POOL_MAX_SIZE);
            pbrMatPool = new ObjectPool<Material>(() => new Material(pbrMaterialReference), actionOnDestroy: UnityObjectUtils.SafeDestroy, defaultCapacity: POOL_INITIAL_CAPACITY, maxSize: POOL_MAX_SIZE);

            destroyMaterial = (in MaterialData data, Material material) => { (data.IsPbrMaterial ? pbrMatPool : basicMatPool).Release(material); };

            instantiationFrameBudgetProvider = singletonSharedDependencies.CapFrameTimeBudgetProvider;

            // materialsCache = new MaterialsCappedCache(CACHE_CAPACITY, (in MaterialData data, Material material) => { (data.IsPbrMaterial ? pbrMatPool : basicMatPool).Release(material); });
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            StartMaterialsLoadingSystem.InjectToWorld(ref builder, destroyMaterial, sharedDependencies.SceneData, LOADING_ATTEMPTS_COUNT);

            // the idea with cache didn't work out: the CPU pressure is too high and benefits are not clear
            // consider revising when and if needed
            // LoadMaterialFromCacheSystem.InjectToWorld(ref builder, materialsCache);
            CreateBasicMaterialSystem.InjectToWorld(ref builder, basicMatPool, instantiationFrameBudgetProvider);
            CreatePBRMaterialSystem.InjectToWorld(ref builder, pbrMatPool, instantiationFrameBudgetProvider);
            ApplyMaterialSystem.InjectToWorld(ref builder);
            ResetMaterialSystem.InjectToWorld(ref builder, destroyMaterial);
            CleanUpMaterialsSystem.InjectToWorld(ref builder, destroyMaterial);
        }
    }
}
