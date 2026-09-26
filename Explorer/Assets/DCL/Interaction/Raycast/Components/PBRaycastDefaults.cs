using DCL.ECSComponents;

namespace DCL.Interaction.Raycast.Components
{
    public static class PBRaycastDefaults
    {
        public static ColliderLayer GetCollisionMask(this PBRaycast self) =>
            self.HasCollisionMask ? (ColliderLayer)(int)self.CollisionMask : ColliderLayer.ClPhysics;
    }
}
