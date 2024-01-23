using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public int CurrentLODLevel;
        public LODAsset CurrentLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;

        public string SceneHash;
        public Vector3 ParcelPosition;
        public bool IsDirty;
        public ParcelMathHelper.SceneCircumscribedPlanes SceneCircumscribedPlanes;

        public void Dispose(World world, ILODAssetsPool lodAssetsPool)
        {
            CurrentLODPromise.ForgetLoading(world);
            lodAssetsPool.Release(CurrentLOD.LodKey, CurrentLOD);
            CurrentLOD = default;
        }

        public string GenerateCurrentLodKey() =>
            SceneHash.ToLower() + "_" + CurrentLODLevel;
    }

}
