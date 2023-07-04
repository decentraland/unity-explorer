using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using UnityEngine;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Intercepts the loading process if the material is in the cache.
    ///     TODO Consider reworking as currently Materials caching is disabled
    /// </summary>
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(StartMaterialsLoadingSystem))]
    [UpdateBefore(typeof(CreatePBRMaterialSystem))]
    [UpdateBefore(typeof(CreateBasicMaterialSystem))]
    [ThrottlingEnabled]
    public partial class LoadMaterialFromCacheSystem : BaseUnityLoopSystem
    {
        private readonly IMaterialsCache materialsCache;

        internal LoadMaterialFromCacheSystem(World world, IMaterialsCache materialsCache) : base(world)
        {
            this.materialsCache = materialsCache;
        }

        protected override void Update(float t)
        {
            TryGetMaterialFromCacheQuery(World);
        }

        [Query]
        private void TryGetMaterialFromCache(ref MaterialComponent materialComponent)
        {
            // Operate only with components for which loading is not started
            if (materialComponent.Status != MaterialComponent.LifeCycle.LoadingNotStarted)
                return;

            if (materialsCache.TryReferenceMaterial(in materialComponent.Data, out Material material))
            {
                materialComponent.Status = MaterialComponent.LifeCycle.LoadingFinished;
                materialComponent.Result = material;
            }
        }
    }
}
