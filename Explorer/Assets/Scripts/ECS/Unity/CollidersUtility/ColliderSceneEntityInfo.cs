using Arch.Core;
using CRDT;
using DCL.ECSComponents;

namespace DCL.Interaction.Utility
{
    /// <summary>
    ///     Entity info associated with a dynamically created collider
    /// </summary>
    public readonly struct ColliderSceneEntityInfo
    {
        public readonly EntityReference EntityReference;
        public readonly CRDTEntity SDKEntity;
        public readonly ColliderLayer SDKLayer;

        public ColliderSceneEntityInfo(EntityReference entityReference, CRDTEntity sdkEntity, ColliderLayer sdkLayer)
        {
            SDKEntity = sdkEntity;
            SDKLayer = sdkLayer;
            EntityReference = entityReference;
        }
    }
}
