using System;
using Arch.Core;
using DCL.Optimization.Pools;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using UnityEngine;
using UnityEngine.Serialization;
using Utility;
using Object = UnityEngine.Object;

namespace ECS.SceneLifeCycle.Components
{
    public struct SceneLODInfo
    {
        public int currentLODLevel;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> currentLODPromise;
        public GameObject currentLOD;

        public void RemoveLOD(World world)
        {
            currentLODPromise.ForgetLoading(world);
            //Derefenrence currentLod stuff
        }

        public void CreateLODPromise(World world, int newLodLevel)
        {
            currentLODLevel = newLodLevel;

            //In case we are loading a lod, we forget it
            currentLODPromise.ForgetLoading(world);
            //.currentLODPromise = AssetPromise<AssetBundleData, GetAssetBundleIntention>().Create()
        }

        public static SceneLODInfo Create(World world, byte partitionBucket, Vector2Int[] bucketLodsLimits)
        {
            SceneLODInfo sceneLODInfo = new SceneLODInfo()
            {
                currentLODLevel = -1, //Ensure that a lod level will be set
            };
            LODUtils.ResolveLODLevel(world, ref sceneLODInfo, partitionBucket, bucketLodsLimits);
            return sceneLODInfo;
        }
    }
    
}