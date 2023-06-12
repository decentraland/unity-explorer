using DCL.ECSComponents;

namespace ECS.Unity.GLTFContainer.Components.Defaults
{
    public static class PBGLTFDefaults
    {
        public static uint GetVisibleMeshesCollisionMask(this PBGltfContainer self) =>
            self.VisibleMeshesCollisionMask;

        public static uint GetInvisibleMeshesCollisionMask(this PBGltfContainer self) =>
            self.HasInvisibleMeshesCollisionMask
                ? self.InvisibleMeshesCollisionMask
                : (int)(ColliderLayer.ClPhysics | ColliderLayer.ClPointer);
    }
}
