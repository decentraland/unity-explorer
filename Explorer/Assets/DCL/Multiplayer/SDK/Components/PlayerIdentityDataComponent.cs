using Arch.Core;
using CRDT;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerIdentityDataComponent
    {
        public ISceneFacade SceneFacade;
        public Entity SceneWorldEntity;
        public CRDTEntity CRDTEntity;
        public string Address;
        public bool IsGuest;
    }
}
