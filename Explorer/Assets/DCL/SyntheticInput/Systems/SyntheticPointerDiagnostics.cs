using Arch.Core;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.Utility;
using DCL.SyntheticInput.Components;
using System.Collections.Generic;
using UnityEngine;
using PlayerOriginatedRaycastSystem = DCL.Interaction.Systems.PlayerOriginatedRaycastSystem;
using RaycastHit = UnityEngine.RaycastHit;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Turns what the reticle pipeline left behind on the observe frame into the driver-facing verdict of a
    ///     synthetic pointer gesture. Pure functions of the pipeline's own raycast and hover state, kept apart from
    ///     <see cref="SyntheticPointerEventSystem" />'s inject/observe/park cycle.
    /// </summary>
    internal static class SyntheticPointerDiagnostics
    {
        public static SyntheticPointerResult Failure(in SyntheticPointerEventIntent intent, string reason) =>
            new ()
            {
                Hit = false,
                FailureReason = reason,
                SceneEntityId = intent.TargetEntityId,
            };

        /// <summary>
        ///     The release must land on the entity that received the press, and a lone event with an explicit target
        ///     on that target. A pure aim-point event accepts whatever the pipeline hit.
        /// </summary>
        public static bool IsExpectedTarget(in SyntheticPointerEventIntent intent, Entity hitEntity)
        {
            if (intent.Press is { } press)
                return hitEntity == press.Entity;

            return intent.TargetEntityId < 0 || hitEntity.Id == intent.TargetEntityId;
        }

        /// <summary>
        ///     The verdict of an aimed gesture from the pipeline's answer for the injected frame. A delivered press
        ///     hands off <paramref name="press" /> so its release can be ordered against it.
        /// </summary>
        public static SyntheticPointerResult BuildResult(in SyntheticPointerEventIntent intent, World sceneWorld,
            in PlayerOriginRaycastResultForSceneEntities raycastResult, in HoverStateComponent hoverState,
            IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips, IEntityCollidersGlobalCache collidersGlobalCache,
            out SyntheticPressHandoff? press)
        {
            press = null;

            // The pipeline echoes the aim it consumed; anything else means the guarded frame ignored the input.
            if (raycastResult.SyntheticAimPoint != intent.InjectedAimPoint)
                return Failure(in intent, "the reticle pipeline did not process the synthetic aim (is the cursor panning or the in-world camera active?)");

            if (!raycastResult.IsValidHit)
                return DiagnoseMiss(in intent, raycastResult.OriginRay, collidersGlobalCache);

            GlobalColliderSceneEntityInfo entityInfo = raycastResult.EntityInfo!.Value;
            Entity hitEntity = entityInfo.ColliderSceneEntityInfo.EntityReference;
            int hitCrdtId = entityInfo.ColliderSceneEntityInfo.SDKEntity.Id;

            if (!ReferenceEquals(entityInfo.EcsExecutor.World, sceneWorld))
                return Failure(in intent, $"the ray landed on a collider of a different scene ('{raycastResult.Collider.name}')");

            if (!IsExpectedTarget(in intent, hitEntity))
            {
                SyntheticPointerResult blocked = Failure(in intent, "another collider blocks the line of sight to the target");
                blocked.BlockedByEntityId = hitEntity.Id;
                blocked.BlockedByCrdtId = hitCrdtId;
                blocked.BlockedByColliderName = raycastResult.Collider.name;
                return blocked;
            }

            if (IsHoveredAtDistance(in raycastResult, in hoverState))
            {
                if (intent.EventType == PointerEventType.PetDown)
                    press = new SyntheticPressHandoff
                    {
                        World = sceneWorld,
                        Entity = hitEntity,
                        Tick = intent.InjectedTick,
                    };

                return Hit(in raycastResult, in entityInfo, tooltips);
            }

            return DiagnoseUnqualified(in intent, in entityInfo, hitEntity, hitCrdtId, raycastResult.GetDistance(),
                StoppedShortOfAim(in raycastResult, intent.InjectedAimPoint), raycastResult.Collider.name);
        }

        /// <summary>
        ///     An aimless button edge has no target to validate, so the result only reports what the cursor ray was
        ///     hovering. A hovered qualified target takes the edge entity-bound, suppressing the global broadcast for
        ///     that scene exactly as a real key press would; otherwise the edge reached the scene root.
        /// </summary>
        public static SyntheticPointerResult BuildAimlessResult(in PlayerOriginRaycastResultForSceneEntities raycastResult, in HoverStateComponent hoverState,
            IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips)
        {
            if (raycastResult is { IsValidHit: true, EntityInfo: { } entityInfo } && IsHoveredAtDistance(in raycastResult, in hoverState))
                return Hit(in raycastResult, in entityInfo, tooltips);

            return new SyntheticPointerResult
            {
                Hit = false,
                SceneEntityId = -1,
                RootBroadcast = true,
            };
        }

        /// <summary>The pipeline hit nothing usable: a cold-path raycast tells whether the aim reaches any collider at all.</summary>
        public static SyntheticPointerResult DiagnoseMiss(in SyntheticPointerEventIntent intent, in Ray originRay, IEntityCollidersGlobalCache collidersGlobalCache)
        {
            if (!Physics.Raycast(originRay, out RaycastHit hit, PlayerOriginatedRaycastSystem.MAX_RAYCAST_DISTANCE, PhysicsLayers.PLAYER_ORIGIN_RAYCAST_MASK))
                return Failure(in intent, "the ray from the camera hit nothing (target may lack a collider)");

            if (collidersGlobalCache.TryGetSceneEntity(hit.collider, out _))
                return Failure(in intent, "the reticle found no scene entity under the aim (transient scene state; retry)");

            return Failure(in intent, DescribeNonSceneHit(in intent, in hit, originRay.origin));
        }

        /// <summary>
        ///     Names a non-scene collider in terms of the aim, not on its own. Reporting only what the ray met reads
        ///     as if that object were in the way, when the usual cause is an aim point with nothing at it: the ray
        ///     passed straight through and met geometry far beyond. The distance to the aim separates the two.
        /// </summary>
        private static string DescribeNonSceneHit(in SyntheticPointerEventIntent intent, in RaycastHit hit, Vector3 origin)
        {
            // A screen-point aim is projected to a far point along the camera ray, so its "aim distance" is the
            // raycast limit and comparing against it says nothing.
            if (intent.ScreenPoint != null)
                return $"nothing clickable at that point: the ray hit non-scene geometry ('{hit.collider.name}')";

            float aimDistance = Vector3.Distance(origin, intent.InjectedAimPoint);

            return hit.distance > aimDistance
                ? $"nothing at the aim point: the ray passed it and hit non-scene geometry ('{hit.collider.name}') {hit.distance - aimDistance:F1} m further on (does the target have a collider?)"
                : $"non-scene geometry ('{hit.collider.name}') blocks the aim {aimDistance - hit.distance:F1} m before it";
        }

        /// <summary>
        ///     The ray reached an entity the gesture accepts as its target, but the pipeline did not qualify it for
        ///     cursor input. For a pure aim-point gesture, an entity without PointerEvents that the ray reached
        ///     before the aim point is an occluder rather than the target, and is reported as a block. That reading
        ///     is confined to aim-point gestures: an entity aim point is the collider's center, which the ray always
        ///     stops short of at the collider's face, and the target must not read as its own occluder.
        /// </summary>
        public static SyntheticPointerResult DiagnoseUnqualified(in SyntheticPointerEventIntent intent, in GlobalColliderSceneEntityInfo entityInfo,
            Entity hitEntity, int hitCrdtId, float distance, bool stoppedShortOfAim, string colliderName)
        {
            SyntheticPointerResult result;
            bool aimPointOnly = intent.TargetEntityId < 0 && !intent.Press.HasValue;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents) || pbPointerEvents == null)
            {
                if (aimPointOnly && stoppedShortOfAim)
                {
                    result = Failure(in intent, "another collider blocks the line of sight to the aim point");
                    result.BlockedByEntityId = hitEntity.Id;
                    result.BlockedByCrdtId = hitCrdtId;
                    result.BlockedByColliderName = colliderName;
                    result.Distance = distance;
                    return result;
                }

                result = Failure(in intent, $"entity {hitEntity.Id} has no PointerEvents component (not clickable)");
            }
            else
                result = Failure(in intent, HasCursorEntry(pbPointerEvents)
                    ? $"target is out of range for its pointer events (hit distance {distance:F2}m)"
                    : "the target's pointer events are proximity-type only and the player is out of proximity range");

            result.SceneEntityId = hitEntity.Id;
            result.CrdtEntityId = hitCrdtId;
            result.Distance = distance;
            return result;
        }

        /// <summary>
        ///     True when the ray was stopped by geometry closer than the point it was aimed through. The
        ///     camera-origin hit distance is the comparable one: the pipeline's own distance is measured from the
        ///     player focus in third person.
        /// </summary>
        public static bool StoppedShortOfAim(in PlayerOriginRaycastResultForSceneEntities raycastResult, Vector3 aimPoint)
        {
            const float TOLERANCE = 0.05f;

            return raycastResult.RaycastHit.distance < Vector3.Distance(raycastResult.OriginRay.origin, aimPoint) - TOLERANCE;
        }

        /// <summary>The hovered collider is the one the ray hit, within the range its pointer events declare.</summary>
        private static bool IsHoveredAtDistance(in PlayerOriginRaycastResultForSceneEntities raycastResult, in HoverStateComponent hoverState) =>
            hoverState.HasCollider && hoverState.LastHitCollider == raycastResult.Collider && hoverState.IsAtDistance;

        private static SyntheticPointerResult Hit(in PlayerOriginRaycastResultForSceneEntities raycastResult, in GlobalColliderSceneEntityInfo entityInfo,
            IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips) =>
            new ()
            {
                Hit = true,
                SceneEntityId = entityInfo.ColliderSceneEntityInfo.EntityReference.Id,
                CrdtEntityId = entityInfo.ColliderSceneEntityInfo.SDKEntity.Id,
                HoverText = ResolveHoverText(in entityInfo, tooltips),
                HitPoint = raycastResult.RaycastHit.point,
                Distance = raycastResult.GetDistance(),
            };

        private static bool HasCursorEntry(PBPointerEvents pbPointerEvents)
        {
            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
                if (pbPointerEvents.PointerEvents[i]!.InteractionType == InteractionType.Cursor)
                    return true;

            return false;
        }

        /// <summary>
        ///     The hover text a human would read on the target. The client's tooltip is preferred but exists only for
        ///     press/release entries, so the target's own PointerEvents text is the fallback — without it,
        ///     hover-only entities report no text at all.
        /// </summary>
        public static string? ResolveHoverText(in GlobalColliderSceneEntityInfo entityInfo, IReadOnlyList<HoverFeedbackComponent.Tooltip> tooltips)
        {
            if (tooltips is { Count: > 0 })
                return tooltips[0].Text;

            if (!entityInfo.TryGetPointerEvents(out PBPointerEvents? pbPointerEvents) || pbPointerEvents == null)
                return null;

            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
            {
                PBPointerEvents.Types.Entry entry = pbPointerEvents.PointerEvents[i]!;

                if (entry.InteractionType == InteractionType.Cursor && entry.EventInfo is { HasHoverText: true } info && !string.IsNullOrEmpty(info.HoverText))
                    return info.HoverText;
            }

            return null;
        }
    }
}
