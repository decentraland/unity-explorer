using CRDT;
using DCL.ECSComponents;

namespace DCL.Multiplayer.SDK.Components
{
    /// <summary>
    ///     Dedicated to the scene world
    /// </summary>
    public struct PlayerSceneCRDTEntity : IDirtyMarker
    {
        public readonly CRDTEntity CRDTEntity;
        public bool IsDirty { get; set; }

        public PlayerSceneCRDTEntity(CRDTEntity crdtEntity)
        {
            CRDTEntity = crdtEntity;
            IsDirty = true;
        }
    }
}
