using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Should exist in the global world only,
    ///     <see cref="CRDTEntity" /> is placed to the scene world.
    /// </summary>
    public struct PlayerCRDTEntity : IDirtyMarker
    {
        public CRDTEntity CRDTEntity { get; }

        public ISceneFacade? SceneFacade { get; private set; }

        public Entity SceneWorldEntity { get; private set; }

        public PlayerCRDTEntity(CRDTEntity crdtEntity) : this()
        {
            CRDTEntity = crdtEntity;
            SceneWorldEntity = Entity.Null;
        }

        public void AssignToScene(ISceneFacade sceneFacade, Entity sceneWorldEntity)
        {
            SceneFacade = sceneFacade;
            SceneWorldEntity = sceneWorldEntity;
            IsDirty = true;
        }

        public void RemoveFromScene()
        {
            SceneFacade = null;
            SceneWorldEntity = Entity.Null;
            IsDirty = true;
        }

        public bool AssignedToScene => SceneFacade != null;

        public bool IsDirty { get; set; }
    }
}
