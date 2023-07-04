using DCL.ECSComponents;

namespace CrdtEcsBridge.Components.Defaults
{
    public static class PBMeshColliderDefaults
    {
        public static float GetTopRadius(this PBMeshCollider.Types.CylinderMesh self) =>
            self.HasRadiusTop ? self.RadiusTop : 0.5f;

        public static float GetBottomRadius(this PBMeshCollider.Types.CylinderMesh self) =>
            self.HasRadiusBottom ? self.RadiusBottom : 0.5f;

        public static ColliderLayer GetColliderLayer(this PBMeshCollider self) =>
            self.HasCollisionMask ? (ColliderLayer)self.CollisionMask : ColliderLayer.ClPhysics | ColliderLayer.ClPointer;
    }
}
