using Arch.Core;
using CRDT;

namespace DCL.SDKComponents.AssetLoad.Components
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
