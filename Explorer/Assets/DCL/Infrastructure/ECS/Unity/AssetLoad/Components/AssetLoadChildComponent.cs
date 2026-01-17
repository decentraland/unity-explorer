using CRDT;

namespace ECS.Unity.AssetLoad.Components
{
    public struct AssetLoadChildComponent
    {
        public readonly CRDTEntity Parent;
        public readonly string AssetHash;
        public readonly string AssetPath;

        public AssetLoadChildComponent(CRDTEntity parent, string assetHash, string assetPath)
        {
            Parent = parent;
            AssetHash = assetHash;
            AssetPath = assetPath;
        }
    }
}
