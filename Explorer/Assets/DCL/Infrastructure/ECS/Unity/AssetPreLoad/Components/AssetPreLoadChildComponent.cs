using CRDT;

namespace ECS.Unity.AssetLoad.Components
{
    public struct AssetPreLoadChildComponent
    {
        public readonly CRDTEntity Parent;
        public readonly string AssetHash;
        public readonly string AssetPath;

        public AssetPreLoadChildComponent(CRDTEntity parent, string assetHash, string assetPath)
        {
            Parent = parent;
            AssetHash = assetHash;
            AssetPath = assetPath;
        }
    }
}
