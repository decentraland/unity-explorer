using Arch.Core;
using AssetManagement;
using CommunicationData.URLHelpers;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData,
        ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;
namespace ECS.SceneLifeCycle.Components
{
    public struct SceneLODInfo
    {
        public int CurrentLODLevel;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;
        public GameObject CurrentLOD;
        public string SceneHash;
        public Vector3 ParcelPosition;

        public void CreateLODPromise(World world, int newLodLevel, PartitionComponent partition)
        {
            if (!CurrentLODPromise.IsConsumed)
                //If we are loading a lod, lets forget it
                CurrentLODPromise.ForgetLoading(world);

            CurrentLODLevel = newLodLevel;
            CurrentLODPromise =
                Promise.Create(world,
                    GetAssetBundleIntention.FromHash($"{SceneHash.ToLower()}_lod{CurrentLODLevel}",
                        permittedSources: AssetSource.EMBEDDED,
                        customEmbeddedSubDirectory: URLSubdirectory.FromString("lods")),
                    partition);

            if (SceneHash.Equals("bafkreieifr7pyaofncd6o7vdptvqgreqxxtcn3goycmiz4cnwz7yewjldq"))
                Debug.Log($"JUANI CREATING LOD PROMISE {SceneHash.ToLower()}_lod{CurrentLODLevel}");
        }

        public void ReleaseCurrentLOD(World world)
        {
            //If its still loading, lets forget it
            //CurrentLODPromise.ForgetLoading(world);

            //If not, dereference it
            if (CurrentLOD != null)
                Object.Destroy(CurrentLOD);
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
            ref PartitionComponent partitionComponent)
        {
            SceneLODInfo sceneLODInfo = new SceneLODInfo()
            {
                CurrentLODLevel = -1 //Ensure that a lod level will be on first resolve
            };
            sceneLODInfo.SceneHash = sceneDefinitionComponent.Definition.id;
            sceneLODInfo.ParcelPosition =
                ParcelMathHelper.GetPositionByParcelPosition(sceneDefinitionComponent.Parcels[0]);
            sceneLODInfo.ResolveLODLevel(world, ref partitionComponent);
            return sceneLODInfo;
        }


    }
    
}