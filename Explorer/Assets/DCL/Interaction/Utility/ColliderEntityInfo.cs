using CRDT;
using DCL.ECSComponents;

namespace DCL.Interaction.Utility
{
    /// <summary>
    ///     Entity info associated with a dynamically created collider
    /// </summary>
    public readonly struct ColliderEntityInfo
    {
        public readonly CRDTEntity Entity;
        public readonly ColliderLayer SDKLayer;

        public ColliderEntityInfo(CRDTEntity entity, ColliderLayer sdkLayer)
        {
            Entity = entity;
            SDKLayer = sdkLayer;
        }
    }
}
