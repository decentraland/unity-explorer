using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Components
{
    public struct PlayerCRDTEntity : IDirtyMarker
    {
        public readonly CRDTEntity CRDTEntity;
        public readonly ISceneFacade SceneFacade;
        public readonly Entity SceneWorldEntity;

        public bool IsDirty { get; set; }

        public PlayerCRDTEntity(CRDTEntity crdtEntity, ISceneFacade sceneFacade, Entity sceneWorldEntity)
        {
            CRDTEntity = crdtEntity;
            SceneFacade = sceneFacade;
            SceneWorldEntity = sceneWorldEntity;
            IsDirty = true;
        }
    }
}
