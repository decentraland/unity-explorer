using Arch.Core;
using Arch.System;
using ECS.Abstract;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public abstract partial class ProcessAssetSystem<T> : BaseUnityLoopSystem where T : Object
    {
        protected ProcessAssetSystem(World world) : base(world)
        {
        }
        
        protected override void Update(float t)
        {
            ProcessAssetQuery(World);
        }

        [Query]
        public void ProcessAsset(Entity entity, in AssetProcessingRequest<T> request)
        {
            ProcessAsset(request.Asset);

            World.Destroy(entity);
        }

        protected abstract void ProcessAsset(T asset);
    }
}
