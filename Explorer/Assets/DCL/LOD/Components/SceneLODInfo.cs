using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
        ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public int CurrentLODLevel;
        public LODAsset CurrentLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public LODCache LodCache;
        
        public string SceneHash;
        public Vector3 ParcelPosition;
        public bool IsDirty;


        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
            LodCache.Dereference(CurrentLOD.LodKey, CurrentLOD);
        }

        public string GetCurrentLodKey()
        {
            return SceneHash.ToLower() + "_lod" + CurrentLODLevel;
        }
    }
    
}