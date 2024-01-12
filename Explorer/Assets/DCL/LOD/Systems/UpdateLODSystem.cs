using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    [UpdateAfter(typeof(UpdateVisualSceneStateSystem))]
    public partial class UpdateLODSystem : BaseUnityLoopSystem
    {
        public UpdateLODSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            UpdateLODLevelQuery(World);
            ResolveCurrentLODQuery(World);
        }
        
        [Query]
        public void UpdateLODLevel(ref SceneLODInfo sceneLODInfo, ref PartitionComponent partitionComponent)
        {
            if (!partitionComponent.IsDirty) return;
            sceneLODInfo.ResolveLODLevel(World, ref partitionComponent);
        }
        
        [Query]
        public void ResolveCurrentLOD(ref SceneLODInfo sceneLODInfo)
        {
            if (sceneLODInfo.CurrentLODPromise.IsConsumed) return;
            if (sceneLODInfo.CurrentLODPromise.TryConsume(World, out StreamableLoadingResult<AssetBundleData> result))
            {
                if (sceneLODInfo.SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log("JUANI I FINISHED THE PROMISE WITH RESULT " + result.Succeeded + " " +
                              sceneLODInfo.CurrentLODLevel);
                if (result.Succeeded)
                {
                    sceneLODInfo.ReleaseCurrentLOD(World);
                    if (sceneLODInfo.SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                        Debug.Log("JUANI IM ABOUT TO INSTANTIATE THE LOD " + sceneLODInfo.CurrentLODLevel);
                    sceneLODInfo.CurrentLOD = Object.Instantiate(result.Asset.GameObject, sceneLODInfo.ParcelPosition,
                        Quaternion.identity);
                }
                //ReportHub.LogError(GetReportCategory(), $"LOD not found for {sceneLODInfo.SceneHash}");
            }
        }

        
    }
}