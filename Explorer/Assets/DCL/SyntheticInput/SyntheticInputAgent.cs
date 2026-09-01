using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.Core;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.SyntheticInput
{
    /// <summary>
    ///     <para>
    ///         Driver-facing entry point of the synthetic input simulation layer: an automation driver (the MCP
    ///         server, AltTester probes) calls it from the main thread to execute the same inputs a human can.
    ///         Requests are delivered by the SyntheticInput systems through the production input pipelines, so
    ///         collisions, occlusion, distance gates, scene input locks and the scene write-back are the real ones.
    ///     </para>
    ///     <para>
    ///         Timeouts are handled here: a request the simulation never completed is abandoned and reported as
    ///         timed out. Requests are last-write-wins — a newer request preempts a pending one of the same kind,
    ///         so one driver at a time is supported.
    ///     </para>
    /// </summary>
    public class SyntheticInputAgent
    {
        /// <summary>Extra wait beyond a hold's own duration before a request is considered stuck.</summary>
        public const float COMPLETION_GRACE_SEC = 5f;

        private readonly World world;
        private readonly Entity playerEntity;

        public SyntheticInputAgent(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        /// <summary>
        ///     Holds a camera-relative movement input on the player for a duration through the real locomotion
        ///     pipeline (velocity, collisions, jumps apply). directionY is forward, directionX is strafe right.
        ///     Scene InputModifier locks apply exactly as they do to WASD unless <paramref name="ignoreInputModifiers" />.
        /// </summary>
        public async UniTask<SyntheticInputDelivery> WalkAsync(Vector2 axes, MovementKind kind, float seconds, bool jump = false,
            bool ignoreInputModifiers = false, CancellationToken ct = default)
        {
            UniTask<SyntheticInputDelivery> hold = EcsRequest.SendAsync(world, playerEntity, new SyntheticMovementIntent
            {
                Axes = axes,
                Kind = kind,
                EndTime = UnityEngine.Time.time + seconds,
                JumpRequested = jump,
                IgnoreInputModifiers = ignoreInputModifiers,
            }, SyntheticInputDelivery.Preempted);

            return await AwaitHoldAsync<SyntheticMovementIntent>(hold, seconds, ct);
        }

        /// <summary>
        ///     Holds a camera-look delta (Cinemachine input-axis value, mouse-look semantics) for a duration.
        ///     Suppressed while a camera blocker is active, exactly like real look input.
        /// </summary>
        public async UniTask<SyntheticInputDelivery> CameraLookAsync(Vector2 axisValue, float seconds, CancellationToken ct = default)
        {
            UniTask<SyntheticInputDelivery> hold = EcsRequest.SendAsync(world, playerEntity, new SyntheticCameraLookIntent
            {
                AxisValue = axisValue,
                EndTime = UnityEngine.Time.time + seconds,
            }, SyntheticInputDelivery.Preempted);

            return await AwaitHoldAsync<SyntheticCameraLookIntent>(hold, seconds, ct);
        }

        /// <summary>Rotates the camera to aim at a world point; completes once the camera consumed the rotation.</summary>
        public async UniTask<SyntheticInputDelivery> LookAtAsync(Vector3 worldTarget, CancellationToken ct = default)
        {
            UniTask<SyntheticInputDelivery> lookAt = EcsRequest.SendAsync(world, playerEntity, new SyntheticCameraLookIntent
            {
                LookAtTarget = worldTarget,
            }, SyntheticInputDelivery.Preempted);

            return await AwaitHoldAsync<SyntheticCameraLookIntent>(lookAt, 0f, ct);
        }

        /// <summary>
        ///     Presses and releases a pointer button on a scene entity (or at an explicit world/screen aim point)
        ///     through the real reticle pipeline. The release is ordered onto a later scene tick than the press, and
        ///     a release that no longer reaches the press target reports the delivered press with the divergence.
        /// </summary>
        public UniTask<SyntheticPointerResult> ClickAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(targetEntityId, sceneId, aimPoint, screenPoint, button, composeClick: true, PointerEventType.PetDown, timeoutSec, force, ct);

        /// <summary>Delivers a lone press leg: the scene observes only the PetDown.</summary>
        public UniTask<SyntheticPointerResult> PointerDownAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(targetEntityId, sceneId, aimPoint, screenPoint, button, composeClick: false, PointerEventType.PetDown, timeoutSec, force, ct);

        /// <summary>Delivers a lone release leg: the scene observes only the PetUp.</summary>
        public UniTask<SyntheticPointerResult> PointerUpAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(targetEntityId, sceneId, aimPoint, screenPoint, button, composeClick: false, PointerEventType.PetUp, timeoutSec, force, ct);

        /// <summary>
        ///     Aims the reticle at a scene entity (or an explicit world/screen point) and holds the hover for a
        ///     duration without pressing anything: the scene observes the same hover enter/leave flow a real
        ///     cursor produces, and the result reports what was hovered.
        /// </summary>
        public async UniTask<SyntheticPointerResult> HoverAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            float seconds, CancellationToken ct = default)
        {
            try
            {
                SyntheticPointerOutcome outcome = await SendPointerAsync(SyntheticPointerEventIntent.Hover(targetEntityId, sceneId, aimPoint, screenPoint, UnityEngine.Time.time + seconds))
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(seconds + COMPLETION_GRACE_SEC));

                return outcome.Result;
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(targetEntityId, seconds + COMPLETION_GRACE_SEC);
            }
        }

        /// <summary>
        ///     <para>
        ///         Presses and releases an SDK input action. Without an aim the cursor ray stays in charge and the
        ///         edges fan out to the scene root — which is what a driver gets in practice, because a driver has
        ///         no OS cursor resting on a target (the reticle ray follows the free cursor, and a free cursor
        ///         hovers nothing the driver chose).
        ///     </para>
        ///     <para>
        ///         Pass an aim (<paramref name="targetEntityId" />, or an explicit <paramref name="aimPoint" />) to
        ///         steer the reticle at a target for the duration of the gesture: the edges then land entity-bound
        ///         on it under the real qualification gates, exactly like a key pressed while looking at it, and
        ///         the result carries the same hit/occlusion/range diagnostics a click does.
        ///     </para>
        ///     <para>
        ///         The release lands on a later scene tick; a positive <paramref name="holdSeconds" /> keeps the
        ///         action held between the edges.
        ///     </para>
        /// </summary>
        public async UniTask<SyntheticPointerResult> GlobalInputAsync(InputAction action, float holdSeconds = 0f,
            int targetEntityId = -1, string? sceneId = null, Vector3? aimPoint = null, CancellationToken ct = default)
        {
            try
            {
                return await RunGlobalGestureAsync(action, holdSeconds, targetEntityId, sceneId, aimPoint, ct)
                            .AttachExternalCancellation(ct)
                            .Timeout(TimeSpan.FromSeconds(holdSeconds + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(targetEntityId, holdSeconds + COMPLETION_GRACE_SEC);
            }
        }

        /// <summary>Awaits a held request with a grace window on top of its duration; a stuck request is abandoned.</summary>
        private async UniTask<SyntheticInputDelivery> AwaitHoldAsync<TIntent>(UniTask<SyntheticInputDelivery> hold, float seconds, CancellationToken ct)
            where TIntent : struct
        {
            try
            {
                return await hold.AttachExternalCancellation(ct)
                                 .Timeout(TimeSpan.FromSeconds(seconds + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                await EcsRequest.AbandonAsync<TIntent>(world, playerEntity);
                return SyntheticInputDelivery.TimedOut;
            }
        }

        private async UniTask<SyntheticPointerResult> RunPointerGestureAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            InputAction button, bool composeClick, PointerEventType firstLegType, float timeoutSec, bool force, CancellationToken ct)
        {
            try
            {
                // A single budget for the whole gesture: it covers both a paused simulation that never runs
                // the delivering system and a release stuck waiting for the scene tick to advance.
                return await ComposeGestureAsync(targetEntityId, sceneId, aimPoint, screenPoint, button, composeClick, firstLegType, force)
                            .AttachExternalCancellation(ct)
                            .Timeout(TimeSpan.FromSeconds(timeoutSec));
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(targetEntityId, timeoutSec);
            }
        }

        /// <summary>
        ///     Composes the requested gesture from single-event intents: a lone press or release is one delivery;
        ///     a click is a press followed by a release that carries the press handoff so the delivering system
        ///     keeps it ordered onto a later scene tick.
        /// </summary>
        private async UniTask<SyntheticPointerResult> ComposeGestureAsync(int targetEntityId, string? sceneId, Vector3? aimPoint, Vector2? screenPoint,
            InputAction button, bool composeClick, PointerEventType firstLegType, bool force)
        {
            SyntheticPointerOutcome down = await SendPointerAsync(new SyntheticPointerEventIntent(targetEntityId, sceneId, aimPoint, button, firstLegType, screenPoint: screenPoint, force: force));

            if (!composeClick || !down.Result.Hit)
                return down.Result;

            SyntheticPointerOutcome up = await SendPointerAsync(new SyntheticPointerEventIntent(targetEntityId, sceneId, aimPoint, button, PointerEventType.PetUp, down.Press, screenPoint, force));

            if (up.Result.Hit)
                return up.Result;

            // The release did not reach the target (whether it missed, a guard rejected it or a newer request
            // preempted it): report the delivered press, flag the divergence and keep the release diagnostics.
            SyntheticPointerResult merged = down.Result;
            merged.UpRayMissed = true;
            merged.FailureReason = $"the release did not reach the target ({up.Result.FailureReason}); the scene received only the press";
            merged.BlockedByEntityId = up.Result.BlockedByEntityId;
            merged.BlockedByCrdtId = up.Result.BlockedByCrdtId;
            merged.BlockedByColliderName = up.Result.BlockedByColliderName;
            return merged;
        }

        /// <summary>
        ///     An aimed gesture goes through the reticle (entity-bound delivery, full diagnostics); an aimless one
        ///     keeps the cursor ray and reaches the scene root. Both order the release onto a later scene tick.
        /// </summary>
        private async UniTask<SyntheticPointerResult> RunGlobalGestureAsync(InputAction action, float holdSeconds,
            int targetEntityId, string? sceneId, Vector3? aimPoint, CancellationToken ct)
        {
            SyntheticPointerOutcome down = await SendPointerAsync(new SyntheticPointerEventIntent(targetEntityId, sceneId, aimPoint, action, PointerEventType.PetDown));

            if (down.Result.FailureReason != null)
                return down.Result;

            if (holdSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(holdSeconds), cancellationToken: ct);

            SyntheticPointerOutcome up = await SendPointerAsync(new SyntheticPointerEventIntent(targetEntityId, sceneId, aimPoint, action, PointerEventType.PetUp, down.Press));

            if (up.Result.FailureReason == null)
                return up.Result;

            SyntheticPointerResult merged = down.Result;
            merged.FailureReason = $"the release was not delivered ({up.Result.FailureReason}); the scene received only the press";
            return merged;
        }

        private async UniTask<SyntheticPointerResult> AbandonPointerAsync(int targetEntityId, float budgetSec)
        {
            await EcsRequest.AbandonAsync<SyntheticPointerEventIntent>(world, playerEntity);

            return new SyntheticPointerResult
            {
                Hit = false,
                TimedOut = true,
                FailureReason = $"the pointer gesture did not complete within {budgetSec}s (is the simulation paused?)",
                SceneEntityId = targetEntityId,
            };
        }

        private UniTask<SyntheticPointerOutcome> SendPointerAsync(SyntheticPointerEventIntent request) =>
            EcsRequest.SendAsync(world, playerEntity, request, new SyntheticPointerOutcome
            {
                Result = new SyntheticPointerResult
                {
                    Hit = false,
                    FailureReason = "preempted by a newer pointer request",
                },
            });
    }
}
