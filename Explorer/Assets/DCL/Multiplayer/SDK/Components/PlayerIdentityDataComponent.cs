using CRDT;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerIdentityDataComponent
    {
        public CRDTEntity CRDTEntity;
        // public int EntityId;

        public string Address;
        public bool IsGuest;
    }
}
