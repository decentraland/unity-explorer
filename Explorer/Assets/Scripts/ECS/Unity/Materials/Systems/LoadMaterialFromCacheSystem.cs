using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Abstract;
using ECS.Unity.Materials.Components;
using System;
using UnityEngine;

namespace ECS.Unity.Materials.Systems
{
    /// <summary>
    ///     Intercepts the loading process if the material is in the cache.
    ///     TODO Consider reworking as currently Materials caching is disabled
    /// </summary>
    [Obsolete("the idea with cache didn't work out: the CPU pressure is too high and benefits are not clear, consider revising when and if needed")]
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(StartMaterialsLoadingSystem))]
    [UpdateBefore(typeof(CreatePBRMaterialSystem))]
    [UpdateBefore(typeof(CreateBasicMaterialSystem))]
    [ThrottlingEnabled]
    public partial class LoadMaterialFromCacheSystem : BaseUnityLoopSystem
    {
        private readonly IMaterialsCache materialsCache;

        public LoadMaterialFromCacheSystem(World world, IMaterialsCache materialsCache) : base(world)
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
            if (materialComponent.Status != StreamableLoading.LifeCycle.LoadingNotStarted)
                return;

            if (materialsCache.TryReferenceMaterial(in materialComponent.Data, out Material material))
            {
                materialComponent.Status = StreamableLoading.LifeCycle.LoadingFinished;
                materialComponent.Result = material;
            }
        }
    }
}
