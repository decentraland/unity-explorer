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
    /// <summary>
    ///     Resolves where a synthetic pointer gesture is aimed before it is injected: the scene entity the gesture
    ///     was promised and the world point the synthetic ray must pass through. Pure functions of the intent and
    ///     the scene world, kept apart from <see cref="SyntheticPointerEventSystem" />'s inject/observe/park cycle.
    /// </summary>
    internal static class SyntheticPointerAim
    {
        private static readonly QueryDescription ALL_ENTITIES = new ();

        /// <summary>
        ///     The entity the gesture was promised, or null when it named none. Resolved before the aim, because the
        ///     posted edge is restricted to it even when an explicit aim point makes its position irrelevant, and a
        ///     target id that resolves to nothing is a failure rather than an aim reporting a phantom blocker.
        /// </summary>
        public static bool TryResolveTargetEntity(in SyntheticPointerEventIntent intent, World sceneWorld, out Entity? targetEntity, out SyntheticPointerResult failure)
        {
            failure = default(SyntheticPointerResult);
            targetEntity = null;

            if (intent.Press is { } press)
            {
                // Liveness was checked before the release was ordered. An aimless press hands off Entity.Null:
                // its release names no entity either.
                if (press.Entity != Entity.Null)
                    targetEntity = press.Entity;

                return true;
            }

            if (intent.TargetEntityId < 0)
                return true;

            int targetId = intent.TargetEntityId;
            Entity found = Entity.Null;

            // TODO: resolve through CrdtEcsSynchronizer.EntitiesMap (O(1)) once MCP entity addressing moves from
            // raw Arch ids to CRDT ids, together with list_scene_entities/get_entity_details/WorldInfo, which scan
            // for the same reason.
            sceneWorld.Query(in ALL_ENTITIES, entity =>
            {
                if (entity.Id == targetId)
                    found = entity;
            });

            if (found == Entity.Null)
            {
                failure = Failure(in intent, $"no entity with id {targetId} in the current scene world");
                return false;
            }

            targetEntity = found;
            return true;
        }

        /// <summary>
        ///     Resolves the world point the synthetic ray must pass through. An explicit aim is taken as is and needs
        ///     no entity; a screen-space aim is projected to a far point along the ray of
        ///     <paramref name="camera" /> through it. Otherwise the aim is the collider center of
        ///     <paramref name="targetEntity" />.
        /// </summary>
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
                // A screen-addressed aim names a pixel, so whatever owns that pixel intercepts it. The world-aim
                // path above keeps the pipeline's UI bypass instead: there the driver named a world target and the
                // cursor's position is irrelevant.
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

            // A release aims at wherever its press target sits now, so the hover follows a target that moved
            // between the legs; a press aims at its own target's collider volume.
            aimPoint = ResolveEntityAimPoint(sceneWorld, target);
            return true;
        }

        public static Vector3 ResolveAimPoint(in SyntheticPointerEventIntent intent, World sceneWorld, Entity targetEntity) =>
            intent.AimPoint ?? ResolveEntityAimPoint(sceneWorld, targetEntity);

        /// <summary>
        ///     Whether the edge was posted without a target entity: a first leg that named none, or a release whose
        ///     press handed off Entity.Null. Only such an edge can have been broadcast to the scene root.
        /// </summary>
        public static bool IsUntargeted(in SyntheticPointerEventIntent intent) =>
            intent.Press is { } press ? press.Entity == Entity.Null : intent.TargetEntityId < 0;

        /// <summary>Aim at the collider volume when available; entity pivots can sit at hinges or bases and miss.</summary>
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
