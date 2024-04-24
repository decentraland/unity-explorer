using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerCRDTEntity : IDirtyMarker
    {
        public CRDTEntity CRDTEntity;
        public ISceneFacade SceneFacade;
        public Entity SceneWorldEntity;
        public bool IsDirty { get; set; }
    }
}
