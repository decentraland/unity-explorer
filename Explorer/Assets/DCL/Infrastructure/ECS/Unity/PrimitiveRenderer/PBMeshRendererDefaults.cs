using DCL.ECSComponents;

namespace ECS.Unity.PrimitiveRenderer
{
    public static class PBMeshRendererDefaults
    {
        public static float GetTopRadius(this PBMeshRenderer.Types.CylinderMesh self) =>
            self.HasRadiusTop ? self.RadiusTop : 0.5f;

        public static float GetBottomRadius(this PBMeshRenderer.Types.CylinderMesh self) =>
            self.HasRadiusBottom ? self.RadiusBottom : 0.5f;
    }
}
