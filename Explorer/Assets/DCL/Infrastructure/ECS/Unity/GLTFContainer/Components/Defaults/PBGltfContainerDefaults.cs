using DCL.ECSComponents;

namespace ECS.Unity.GLTFContainer.Components.Defaults
{
    // ReSharper disable once InconsistentNaming
    public static class PBGltfContainerDefaults
    {
        public static ColliderLayer GetVisibleMeshesCollisionMask(this PBGltfContainer self) =>
            (ColliderLayer)self.VisibleMeshesCollisionMask;

        public static ColliderLayer GetInvisibleMeshesCollisionMask(this PBGltfContainer self) =>
            self.HasInvisibleMeshesCollisionMask
                ? (ColliderLayer)self.InvisibleMeshesCollisionMask
                : ColliderLayer.ClPhysics | ColliderLayer.ClPointer;
    }
}
