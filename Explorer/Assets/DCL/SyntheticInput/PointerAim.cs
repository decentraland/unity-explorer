using UnityEngine;

namespace DCL.SyntheticInput
{
    /// <summary>What a pointer gesture is aimed at. The default value is the aimless aim.</summary>
    public readonly struct PointerAim
    {
        /// <summary>Arch entity id in the scene world. Null when the aim names no entity.</summary>
        public readonly int? EntityId;

        /// <summary>Scene definition id that the gesture is pinned to. Null accepts the current scene.</summary>
        public readonly string? SceneId;

        /// <summary>World-space aim point. Defaults to the collider center of the entity.</summary>
        public readonly Vector3? AimPoint;

        /// <summary>Screen-space aim point, in Unity screen coordinates.</summary>
        public readonly Vector2? ScreenPoint;

        public PointerAim(int? entityId, string? sceneId = null, Vector3? aimPoint = null, Vector2? screenPoint = null)
        {
            EntityId = entityId;
            SceneId = sceneId;
            AimPoint = aimPoint;
            ScreenPoint = screenPoint;
        }

        public static PointerAim None => default(PointerAim);

        public static PointerAim AtEntity(int entityId, string? sceneId = null, Vector3? aimPoint = null) =>
            new (entityId, sceneId, aimPoint);

        public static PointerAim AtWorldPoint(Vector3 aimPoint, string? sceneId = null) =>
            new (null, sceneId, aimPoint);

        public static PointerAim AtScreenPoint(Vector2 screenPoint, string? sceneId = null) =>
            new (null, sceneId, null, screenPoint);

        public bool HasTarget => EntityId.HasValue || AimPoint.HasValue || ScreenPoint.HasValue;
    }
}
