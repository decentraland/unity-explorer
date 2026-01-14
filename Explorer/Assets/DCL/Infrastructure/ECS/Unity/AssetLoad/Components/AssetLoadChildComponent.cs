using CRDT;

namespace ECS.Unity.AssetLoad.Components
{
    public struct AssetLoadChildComponent
    {
        public CRDTEntity Parent;

        public AssetLoadChildComponent(CRDTEntity parent)
        {
            Parent = parent;
        }
    }
}
