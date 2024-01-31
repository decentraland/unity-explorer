using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using Utility;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(UpdateVisualSceneStateSystem))]
    public partial class ResolveSceneLODInfo : BaseUnityLoopSystem
    {
        private readonly LODAssetsPool lodCache;

        public ResolveSceneLODInfo(World world, LODAssetsPool lodCache) : base(world)
        {
            this.lodCache = lodCache;
        }

        //TODO: This system will resolve the ABManifest when its uploaded
        protected override void Update(float t)
        {
            //ResolveLODInfoQuery(World);
        }

    }
}
