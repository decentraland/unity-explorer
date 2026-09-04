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
    ///     synthetic pointer gesture: which entity the gesture accepts as its target, why a miss or an unqualified
    ///     hit happened, and the hover text a human would have read. Pure functions of the pipeline's own raycast
    ///     and hover state, kept apart from <see cref="SyntheticPointerEventSystem" />'s inject/observe/park cycle.
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
        ///     The release must land on the entity that received the press; a lone event with an explicit target
        ///     must land on that target. A pure aim-point event accepts whatever the pipeline hit.
        /// </summary>
        public static bool IsExpectedTarget(in SyntheticPointerEventIntent intent, Entity hitEntity)
        {
            if (intent.Press is { } press)
                return hitEntity == press.Entity;

            return intent.TargetEntityId < 0 || hitEntity.Id == intent.TargetEntityId;
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
        ///     Names a non-scene collider in terms of the aim, not on its own. Reporting only what the ray met
        ///     ("the ray hit a non-scene collider 'SatelliteView 7,7'") reads as if that object were in the way,
        ///     when the usual cause is an aim point with nothing at it: the ray passed straight through and met the
        ///     skybox geometry far beyond. The distance to the aim separates the two.
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
        ///     cursor input. An entity without PointerEvents that the ray reached <em>before</em> the requested aim
        ///     point is an occluder, not the target — reported as a block so an aim-point gesture gets the same
        ///     blocker diagnostics an entity-addressed one does. That reading only exists for a pure aim-point
        ///     gesture: a gesture with an explicit target reached this method because the hit IS that target
        ///     (anything else was reported as a block upstream), and an entity aim point is the collider's center,
        ///     which the ray always stops short of at the collider's face — the target must not read as its own
        ///     occluder.
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
        ///     True when the ray was stopped by geometry closer than the point it was aimed through: the camera-origin
        ///     hit distance is the comparable one (the pipeline's own distance is measured from the player focus in
        ///     third person).
        /// </summary>
        public static bool StoppedShortOfAim(in PlayerOriginRaycastResultForSceneEntities raycastResult, Vector3 aimPoint)
        {
            const float TOLERANCE = 0.05f;

            return raycastResult.RaycastHit.distance < Vector3.Distance(raycastResult.OriginRay.origin, aimPoint) - TOLERANCE;
        }

        private static bool HasCursorEntry(PBPointerEvents pbPointerEvents)
        {
            for (var i = 0; i < pbPointerEvents.PointerEvents!.Count; i++)
                if (pbPointerEvents.PointerEvents[i]!.InteractionType == InteractionType.Cursor)
                    return true;

            return false;
        }

        /// <summary>
        ///     The hover text a human would read on the target. The client's tooltip is preferred, but it only
        ///     exists for press/release entries (a hover-only entity shows no key prompt), so the target's own
        ///     PointerEvents text is the fallback — otherwise hover-only entities report no text at all.
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
