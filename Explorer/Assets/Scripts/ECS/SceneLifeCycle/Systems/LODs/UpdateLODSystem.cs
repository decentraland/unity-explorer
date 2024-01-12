using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using ECS.Abstract;
using ECS.SceneLifeCycle.Components;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Realm;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(UpdateLODLevelSystem))]
    //TODO: System that will resolve lod state (Manifest and ABs)
    public partial class ResolveLODContentSystem : BaseUnityLoopSystem
    {
        public ResolveLODContentSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
        }
        
        
        [Query]
        public void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent)
        {
            if (!partitionComponent.IsDirty) return;
            LODUtils.ResolveLODLevel(World, ref sceneLODInfo, partitionComponent.Bucket, lodBucketLimits);
        }
        
        [Query]
        public void ResolveCurrentLOD(ref SceneLODInfo sceneLODInfo)
        {
            if (sceneLODInfo.currentLODPromise.IsConsumed) return;

            if (sceneLODInfo.currentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                //TODO: Clear previous LOD. 
                //sceneLODInfo.currentLOD
                sceneLODInfo.currentLOD = GameObject.Instantiate(result.Asset.GameObject);
            }
        }

        
    }
}