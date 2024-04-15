using Arch.Core;
using CRDT;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerIdentityDataComponent
    {
        public Entity SceneWorldEntity;
        public CRDTEntity CRDTEntity;
        public string Address;
        public bool IsGuest;
    }
}
