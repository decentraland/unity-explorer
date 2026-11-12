using Arch.Core;
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
        public readonly Entity GlobalEntity;

        public bool IsDirty { get; set; }

        public PlayerSceneCRDTEntity(CRDTEntity crdtEntity, Entity globalEntity)
        {
            CRDTEntity = crdtEntity;
            GlobalEntity = globalEntity;
            IsDirty = true;
        }
    }
}
