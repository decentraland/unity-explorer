using Arch.Core;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using UnityEngine.Serialization;

namespace DCL.LOD.Components
{
    public struct RoadInfo
    {
        public bool IsDirty;
        public AssetBundleData AssetBundleReference;
        public AssetPromise<AssetBundleData, GetAssetBundleIntention> CurrentRoadPromise;
 
        public static RoadInfo Create() =>
            new()
            {
                IsDirty = true
            };

        public void Dispose(World world)
        {
            CurrentRoadPromise.ForgetLoading(world);
            
            AssetBundleReference.Dereference();
            AssetBundleReference = null;
        }
    }
}