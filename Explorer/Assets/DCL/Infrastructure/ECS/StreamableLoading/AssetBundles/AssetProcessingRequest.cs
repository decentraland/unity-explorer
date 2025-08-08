using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct AssetProcessingRequest<T> where T : Object
    {
        public T Asset;
    }
}
