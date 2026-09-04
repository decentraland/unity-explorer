using UnityEngine;

namespace DCL.SyntheticInput
{
    /// <summary>
    ///     What a pointer gesture is aimed at: a scene entity, a world point, a screen point, or nothing at all (an
    ///     aimless edge keeps the cursor ray and fans out to the scene root like a real key press). The default
    ///     value is the aimless aim, so a forgotten argument can never turn into a target.
    /// </summary>
    public readonly struct PointerAim
    {
        /// <summary>Arch entity id, in the current scene world, of the entity the gesture is promised to; null when it names none.</summary>
        public readonly int? EntityId;

        /// <summary>
        ///     Pins delivery to one scene, matched by the definition id get_scene_state reports: the gesture fails
        ///     instead of landing in whatever scene is current if the player moved. Null accepts the current scene.
        /// </summary>
        public readonly string? SceneId;

        /// <summary>Explicit world-space aim point; when null and an entity is named, the aim is its collider center.</summary>
        public readonly Vector3? AimPoint;

        /// <summary>Explicit screen-space aim, in Unity screen coordinates: the ray is built through this pixel.</summary>
        public readonly Vector2? ScreenPoint;

        public PointerAim(int? entityId, string? sceneId = null, Vector3? aimPoint = null, Vector2? screenPoint = null)
        {
            EntityId = entityId;
            SceneId = sceneId;
            AimPoint = aimPoint;
            ScreenPoint = screenPoint;
        }

        /// <summary>The aimless aim: the cursor ray stays in charge and the edge reaches the scene root.</summary>
        public static PointerAim None => default(PointerAim);

        public static PointerAim AtEntity(int entityId, string? sceneId = null, Vector3? aimPoint = null) =>
            new (entityId, sceneId, aimPoint);

        public static PointerAim AtWorldPoint(Vector3 aimPoint, string? sceneId = null) =>
            new (null, sceneId, aimPoint);

        public static PointerAim AtScreenPoint(Vector2 screenPoint, string? sceneId = null) =>
            new (null, sceneId, null, screenPoint);

        /// <summary>False for the aimless aim.</summary>
        public bool HasTarget => EntityId.HasValue || AimPoint.HasValue || ScreenPoint.HasValue;
    }
}
