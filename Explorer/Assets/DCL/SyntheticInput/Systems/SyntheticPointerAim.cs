using Arch.Core;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.UiSimulation;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using static DCL.SyntheticInput.Systems.SyntheticPointerDiagnostics;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>Resolves the target entity and the world point that a synthetic pointer gesture is aimed at.</summary>
    internal static class SyntheticPointerAim
    {
        private static readonly QueryDescription ALL_ENTITIES = new ();

        /// <summary>Resolved even when an explicit aim point is given, because the posted edge is restricted to the named entity.</summary>
        public static bool TryResolveTargetEntity(in SyntheticPointerEventIntent intent, World sceneWorld, out Entity? targetEntity, out SyntheticPointerResult failure)
        {
            failure = default(SyntheticPointerResult);
            targetEntity = null;

            if (intent.Press is { } press)
            {
                if (press.Entity != Entity.Null)
                    targetEntity = press.Entity;

                return true;
            }

            if (intent.TargetEntityId < 0)
                return true;

            int targetId = intent.TargetEntityId;

            // TODO: resolve through CrdtEcsSynchronizer.EntitiesMap (O(1)) once MCP entity addressing moves from
            // raw Arch ids to CRDT ids, together with list_scene_entities/get_entity_details/WorldInfo, which scan
            // for the same reason. Chunk iteration keeps the scan free of the World.Query delegate allocation.
            foreach (ref Chunk chunk in sceneWorld.Query(in ALL_ENTITIES).GetChunkIterator())
            {
                foreach (int i in chunk)
                {
                    Entity candidate = chunk.Entity(i);

                    if (candidate.Id != targetId)
                        continue;

                    targetEntity = candidate;
                    return true;
                }
            }

            failure = Failure(in intent, $"no entity with id {targetId} in the current scene world");
            return false;
        }

        public static bool TryResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity? targetEntity, Camera camera, UiCoverProbe? uiCoverProbe,
            out Vector3 aimPoint, out SyntheticPointerResult failure)
        {
            failure = default(SyntheticPointerResult);
            aimPoint = default(Vector3);

            if (intent.AimPoint is { } explicitAim)
            {
                aimPoint = explicitAim;
                return true;
            }

            if (intent.ScreenPoint is { } screenPoint)
            {
                // Only a screen-point aim can be covered by UI. A world aim must keep aiming through UI.
                if (!intent.Force && uiCoverProbe != null && uiCoverProbe(screenPoint, out string cover))
                {
                    failure = Failure(in intent, $"UI covers that point ({cover}); click the element with ui_click, or pass force to aim through it");
                    failure.BlockedByUi = cover;
                    return false;
                }

                aimPoint = camera.ScreenPointToRay(screenPoint).GetPoint(PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE);
                return true;
            }

            if (targetEntity is not { } target)
            {
                failure = Failure(in intent, "the gesture names neither an aim point nor a target entity");
                return false;
            }

            // Resolved at release time too, so a target that moved since the press is followed.
            aimPoint = ResolveEntityAimPoint(sceneWorld, target);
            return true;
        }

        public static Vector3 ResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity targetEntity) =>
            intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

        public static bool IsUntargeted(in SyntheticPointerEventIntent intent) =>
            intent.Press is { } press ? press.Entity == Entity.Null : intent.TargetEntityId < 0;

        // The collider center is preferred because an entity pivot can sit at a hinge or a base and miss.
        private static Vector3 ResolveEntityAimPoint(World sceneWorld, Entity entity)
        {
            if (sceneWorld.TryGet(entity, out PrimitiveColliderComponent primitiveCollider) && primitiveCollider.Collider != null)
                return primitiveCollider.Collider.bounds.center;

            if (sceneWorld.TryGet(entity, out TransformComponent transformComponent) && transformComponent.Transform != null)
                return transformComponent.Transform.position;

            return Vector3.zero;
        }
    }
}
