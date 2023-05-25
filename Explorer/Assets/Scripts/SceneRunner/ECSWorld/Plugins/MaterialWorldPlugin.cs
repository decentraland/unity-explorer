using Arch.Core;
using Arch.SystemGroups;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Systems;
using UnityEngine;

namespace SceneRunner.ECSWorld.Plugins
{
    public class MaterialsPlugin : IECSWorldPlugin
    {
        private const int CACHE_CAPACITY = 512;
        private const int LOADING_ATTEMPTS_COUNT = 6;

        private readonly IMaterialsCache materialsCache;

        public MaterialsPlugin()
        {
            materialsCache = new MaterialsCappedCache(CACHE_CAPACITY, Object.Destroy);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies)
        {
            StartMaterialsLoadingSystem.InjectToWorld(ref builder, materialsCache, sharedDependencies.ContentProvider);
            LoadMaterialFromCacheSystem.InjectToWorld(ref builder, materialsCache);
            CreateBasicMaterialSystem.InjectToWorld(ref builder, materialsCache, LOADING_ATTEMPTS_COUNT);
            CreatePBRMaterialSystem.InjectToWorld(ref builder, materialsCache, LOADING_ATTEMPTS_COUNT);
            CleanUpMaterialsSystem.InjectToWorld(ref builder, materialsCache);
        }
    }
}
