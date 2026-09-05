using Arch.Core;

namespace DCL.Interaction.Utility
{
    /// <summary>
    /// Used for detecting colliders in the global world (like for example other avatars).
    /// </summary>
    public readonly struct GlobalColliderGlobalEntityInfo
    {
        public readonly Entity EntityReference;

        public GlobalColliderGlobalEntityInfo(Entity entityReference)
        {
            EntityReference = entityReference;
        }
    }
}
