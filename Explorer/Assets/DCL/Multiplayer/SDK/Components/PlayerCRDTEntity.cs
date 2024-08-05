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
        public readonly CRDTEntity CRDTEntity;
        public readonly ISceneFacade SceneFacade;
        public readonly Entity SceneWorldEntity;
        public readonly bool SceneEntityIsPersistent;

        public PlayerCRDTEntity(CRDTEntity crdtEntity, ISceneFacade sceneFacade, Entity sceneWorldEntity, bool sceneEntityIsPersistent = false)
        {
            CRDTEntity = crdtEntity;
            SceneFacade = sceneFacade;
            SceneWorldEntity = sceneWorldEntity;
            SceneEntityIsPersistent = sceneEntityIsPersistent;
            IsDirty = true;
        }

        public bool IsDirty { get; set; }
    }
}
