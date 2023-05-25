using DCL.ECSComponents;

namespace CrdtEcsBridge.Components.Defaults
{
    public static class PBMeshColliderDefaults
    {
        public static float GetTopRadius(this PBMeshCollider.Types.CylinderMesh self) =>
            self.HasRadiusTop ? self.RadiusTop : 0.5f;

        public static float GetBottomRadius(this PBMeshCollider.Types.CylinderMesh self) =>
            self.HasRadiusBottom ? self.RadiusBottom : 0.5f;

        public static uint GetColliderLayer(this PBMeshCollider self) =>
            self.HasCollisionMask ? self.CollisionMask : (uint)ColliderLayer.ClPhysics | (uint)ColliderLayer.ClPointer;
    }

    public static class PBMeshRendererDefaults
    {
        public static float GetTopRadius(this PBMeshRenderer.Types.CylinderMesh self) =>
            self.HasRadiusTop ? self.RadiusTop : 0.5f;

        public static float GetBottomRadius(this PBMeshRenderer.Types.CylinderMesh self) =>
            self.HasRadiusBottom ? self.RadiusBottom : 0.5f;
    }
}
