using DCL.ECSComponents;

namespace DCL.SDKComponents.TriggerArea.Components
{
    public static class PBTriggerAreaDefaults
    {
        public static ColliderLayer GetColliderLayer(this PBTriggerArea self) =>
            self.HasCollisionMask ? (ColliderLayer)self.CollisionMask : ColliderLayer.ClPlayer;

        public static TriggerAreaMeshType GetMeshType(this PBTriggerArea self) =>
            self.HasMesh ? self.Mesh : TriggerAreaMeshType.TamtBox;
    }
}
