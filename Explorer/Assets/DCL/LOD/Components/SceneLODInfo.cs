using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine;
using Utility;

namespace DCL.LOD.Components
{
    public struct SceneLODInfo
    {
        public string SceneHash;
        public byte CurrentLODLevel;
        public LODAsset? CurrentLOD;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentLODPromise;

        public Vector3 ParcelPosition;
        public bool IsDirty;
        public ParcelMathHelper.SceneCircumscribedPlanes SceneCircumscribedPlanes;

        public void Dispose(World world, ILODAssetsPool lodAssetsPool)
        {
            CurrentLODPromise.ForgetLoading(world);

            if (CurrentLOD != null)
                CurrentLOD.TryRelease(lodAssetsPool);

            CurrentLOD = null;
        }
    }

}
