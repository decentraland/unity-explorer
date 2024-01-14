using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
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
        
        public string SceneHash;
        public Vector3 ParcelPosition;

        private LODCache lodCache;

        public void CreateLODPromise(World world, int newLodLevel, PartitionComponent partition)
        {
            if (!CurrentLODPromise.IsConsumed)
                CurrentLODPromise.ForgetLoading(world);
            
            CurrentLODLevel = newLodLevel;

            var newLODKey = SceneHash + "_" + CurrentLODLevel;
            if (lodCache.TryGet(newLODKey, out var cachedAsset))
            {
                lodCache.Dereference(CurrentLOD.LodKey, CurrentLOD);
                CurrentLOD = cachedAsset;
            }
            else
            {
                CurrentLODPromise =
                    Promise.Create(world,
                        GetAssetBundleIntention.FromHash($"{SceneHash.ToLower()}_lod{CurrentLODLevel}",
                            permittedSources: AssetSource.EMBEDDED,
                            customEmbeddedSubDirectory: URLSubdirectory.FromString("lods")),
                        partition);

                if (SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                    Debug.Log($"JUANI CREATING LOD PROMISE {SceneHash.ToLower()}_lod{CurrentLODLevel}");
            }
        }

        public void ResolveLODLevel(World world, ref PartitionComponent partitionComponent)
        {
            var sceneLODCandidate = 0;
            if (partitionComponent.Bucket > VisualSceneStateConstants.LODS_BUCKET_LIMITS[0][0] &&
                partitionComponent.Bucket <= VisualSceneStateConstants.LODS_BUCKET_LIMITS[0][1])
                sceneLODCandidate = 2;
            else if (partitionComponent.Bucket > VisualSceneStateConstants.LODS_BUCKET_LIMITS[1][0])
                sceneLODCandidate = 3;

            if (sceneLODCandidate != CurrentLODLevel)
            {
                CreateLODPromise(world, sceneLODCandidate, partitionComponent);
            }
        }

        public static SceneLODInfo Create(World world, ref SceneDefinitionComponent sceneDefinitionComponent,
            ref PartitionComponent partitionComponent, LODCache lodCache)
        {
            SceneLODInfo sceneLODInfo = new SceneLODInfo()
            {
                CurrentLODLevel = -1, //Ensure that a lod level will be on first resolve
                SceneHash = sceneDefinitionComponent.Definition.id,
                ParcelPosition = ParcelMathHelper.GetPositionByParcelPosition(sceneDefinitionComponent.Parcels[0]),
                lodCache = lodCache
            };
            sceneLODInfo.ResolveLODLevel(world, ref partitionComponent);
            return sceneLODInfo;
        }


        public void Dispose(World world)
        {
            CurrentLODPromise.ForgetLoading(world);
            lodCache.Dereference(CurrentLOD.LodKey, CurrentLOD);
        }
    }
    
}