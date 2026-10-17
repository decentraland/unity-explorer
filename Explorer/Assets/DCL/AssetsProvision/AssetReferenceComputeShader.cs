using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AssetsProvision
{
    [Serializable]
    public class AssetReferenceComputeShader : AssetReferenceT<ComputeShader>
    {
        public AssetReferenceComputeShader(string guid) : base(guid) { }
    }
}
