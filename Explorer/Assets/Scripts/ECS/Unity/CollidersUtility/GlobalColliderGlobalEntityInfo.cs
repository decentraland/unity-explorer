using Arch.Core;

namespace DCL.Interaction.Utility
{
    public readonly struct GlobalColliderGlobalEntityInfo
    {
        public readonly EntityReference EntityReference;

        public GlobalColliderGlobalEntityInfo(EntityReference entityReference)
        {
            EntityReference = entityReference;
        }
    }
}
