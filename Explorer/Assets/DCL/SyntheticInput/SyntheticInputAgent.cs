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
    ///     Driver-facing entry point of the synthetic input simulation layer. Main thread only. A newer request
    ///     preempts a pending request of the same kind, so only one driver can run at a time.
    /// </summary>
    public class SyntheticInputAgent
    {
        /// <summary>Extra wait beyond a hold's own duration before a request is considered stuck.</summary>
        public const float COMPLETION_GRACE_SEC = 5f;

        // the intent's value for an aim that names no entity
        private const int NO_ENTITY = -1;

        private readonly World world;
        private readonly Entity playerEntity;

        public SyntheticInputAgent(World world, Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        /// <summary>Holds a camera-relative movement input for a duration (x = strafe right, y = forward).</summary>
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

        /// <summary>Holds a camera-look delta (Cinemachine input-axis value) for a duration.</summary>
        public async UniTask<SyntheticInputDelivery> CameraLookAsync(Vector2 axisValue, float seconds, CancellationToken ct = default)
        {
            UniTask<SyntheticInputDelivery> hold = EcsRequest.SendAsync(world, playerEntity, new SyntheticCameraLookIntent
            {
                AxisValue = axisValue,
                EndTime = UnityEngine.Time.time + seconds,
            }, SyntheticInputDelivery.Preempted);

            return await AwaitHoldAsync<SyntheticCameraLookIntent>(hold, seconds, ct);
        }

        /// <summary>Rotates the camera to aim at a world point. Completes once the camera is on target or the correction gives up.</summary>
        public async UniTask<SyntheticInputDelivery> LookAtAsync(Vector3 worldTarget, CancellationToken ct = default)
        {
            UniTask<SyntheticInputDelivery> lookAt = EcsRequest.SendAsync(world, playerEntity, new SyntheticCameraLookIntent
            {
                LookAtTarget = worldTarget,
            }, SyntheticInputDelivery.Preempted);

            return await AwaitHoldAsync<SyntheticCameraLookIntent>(lookAt, 0f, ct);
        }

        /// <summary>Presses and releases a pointer button on what <paramref name="aim" /> names. The release lands on a later scene tick than the press.</summary>
        public UniTask<SyntheticPointerResult> ClickAsync(PointerAim aim, InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(aim, button, composeClick: true, PointerEventType.PetDown, timeoutSec, force, ct);

        /// <summary>Delivers a press without a release.</summary>
        public UniTask<SyntheticPointerResult> PointerDownAsync(PointerAim aim, InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(aim, button, composeClick: false, PointerEventType.PetDown, timeoutSec, force, ct);

        /// <summary>Delivers a release without a press.</summary>
        public UniTask<SyntheticPointerResult> PointerUpAsync(PointerAim aim, InputAction button, float timeoutSec, CancellationToken ct = default, bool force = false) =>
            RunPointerGestureAsync(aim, button, composeClick: false, PointerEventType.PetUp, timeoutSec, force, ct);

        /// <summary>
        ///     Presses on a target, turns the camera while the button is held, then releases. If the press was not
        ///     delivered, the gesture stops there, because a release without a press would fake a delivery.
        /// </summary>
        public async UniTask<SyntheticSweepResult> SweepAsync(PointerAim aim, InputAction button, Vector2 axisValue, float seconds, float timeoutSec,
            CancellationToken ct = default, bool force = false)
        {
            SyntheticPointerResult press = await PointerDownAsync(aim, button, timeoutSec, ct, force);

            if (press.TimedOut || press.FailureReason != null)
                return new SyntheticSweepResult
                {
                    Press = press,
                    FailureReason = press.TimedOut
                        ? $"the press did not complete within {timeoutSec}s, so nothing was held to sweep with"
                        : $"the press was not delivered ({press.FailureReason}), so nothing was held to sweep with",
                };

            SyntheticInputDelivery cameraSweep = await CameraLookAsync(axisValue, seconds, ct);

            return new SyntheticSweepResult
            {
                Press = press,
                CameraSweep = cameraSweep,
                Release = await PointerUpAsync(aim, button, timeoutSec, ct, force),
            };
        }

        /// <summary>Aims at what <paramref name="aim" /> names and holds the hover for a duration. Presses nothing.</summary>
        public async UniTask<SyntheticPointerResult> HoverAsync(PointerAim aim, float seconds, CancellationToken ct = default)
        {
            try
            {
                SyntheticPointerOutcome outcome = await SendPointerAsync(SyntheticPointerEventIntent.Hover(aim.EntityId ?? NO_ENTITY, aim.SceneId, aim.AimPoint, aim.ScreenPoint, UnityEngine.Time.time + seconds))
                                                       .AttachExternalCancellation(ct)
                                                       .Timeout(TimeSpan.FromSeconds(seconds + COMPLETION_GRACE_SEC));

                return outcome.Result;
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(aim, seconds + COMPLETION_GRACE_SEC);
            }
        }

        /// <summary>
        ///     Presses and releases an SDK input action. With an aimless <paramref name="aim" /> the edges reach the
        ///     scene root. With a target they land on that entity, like a click. The release lands on a later scene tick.
        /// </summary>
        public async UniTask<SyntheticPointerResult> GlobalInputAsync(InputAction action, float holdSeconds = 0f, PointerAim aim = default, CancellationToken ct = default)
        {
            try
            {
                return await RunGlobalGestureAsync(action, holdSeconds, aim, ct)
                            .AttachExternalCancellation(ct)
                            .Timeout(TimeSpan.FromSeconds(holdSeconds + COMPLETION_GRACE_SEC));
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(aim, holdSeconds + COMPLETION_GRACE_SEC);
            }
        }

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

        private async UniTask<SyntheticPointerResult> RunPointerGestureAsync(PointerAim aim, InputAction button, bool composeClick, PointerEventType firstLegType,
            float timeoutSec, bool force, CancellationToken ct)
        {
            try
            {
                return await ComposeGestureAsync(aim, button, composeClick, firstLegType, force)
                            .AttachExternalCancellation(ct)
                            .Timeout(TimeSpan.FromSeconds(timeoutSec));
            }
            catch (TimeoutException)
            {
                return await AbandonPointerAsync(aim, timeoutSec);
            }
        }

        private async UniTask<SyntheticPointerResult> ComposeGestureAsync(PointerAim aim, InputAction button, bool composeClick, PointerEventType firstLegType, bool force)
        {
            SyntheticPointerOutcome down = await SendPointerAsync(Intent(in aim, button, firstLegType, force: force));

            if (firstLegType == PointerEventType.PetDown && !down.Result.Hit)
                return await ReleaseBroadcastPressAsync(aim, button, down, force);

            if (!composeClick || !down.Result.Hit)
                return down.Result;

            SyntheticPointerOutcome up = await SendPointerAsync(Intent(in aim, button, PointerEventType.PetUp, down.Press, force));

            if (up.Result.Hit)
                return up.Result;

            SyntheticPointerResult merged = down.Result;
            merged.UpRayMissed = true;
            merged.FailureReason = $"the release did not reach the target ({up.Result.FailureReason}); the scene received only the press";
            merged.BlockedByEntityId = up.Result.BlockedByEntityId;
            merged.BlockedByCrdtId = up.Result.BlockedByCrdtId;
            merged.BlockedByColliderName = up.Result.BlockedByColliderName;
            return merged;
        }

        // A press that missed its target can still reach the scene root as a broadcast. Release it, or the root
        // holds a button that nobody releases.
        private async UniTask<SyntheticPointerResult> ReleaseBroadcastPressAsync(PointerAim aim, InputAction button, SyntheticPointerOutcome down, bool force)
        {
            SyntheticPointerResult result = down.Result;

            if (!down.Press.HasValue)
                return result;

            SyntheticPointerOutcome up = await SendPointerAsync(Intent(in aim, button, PointerEventType.PetUp, down.Press, force));

            if (up.Result.RootBroadcast)
                return result;

            result.FailureReason += up.Result.Hit
                ? $"; the scene root received the press, and its release landed on entity {up.Result.SceneEntityId} instead"
                : $"; the scene root received the press but not its release ({up.Result.FailureReason}), so it is left holding the button";

            return result;
        }

        private async UniTask<SyntheticPointerResult> RunGlobalGestureAsync(InputAction action, float holdSeconds, PointerAim aim, CancellationToken ct)
        {
            SyntheticPointerOutcome down = await SendPointerAsync(Intent(in aim, action, PointerEventType.PetDown));

            if (down.Result.FailureReason != null)
                return down.Result;

            if (holdSeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(holdSeconds), cancellationToken: ct);

            SyntheticPointerOutcome up = await SendPointerAsync(Intent(in aim, action, PointerEventType.PetUp, down.Press));

            if (up.Result.FailureReason == null)
                return up.Result;

            SyntheticPointerResult merged = down.Result;
            merged.FailureReason = $"the release was not delivered ({up.Result.FailureReason}); the scene received only the press";
            return merged;
        }

        private async UniTask<SyntheticPointerResult> AbandonPointerAsync(PointerAim aim, float budgetSec)
        {
            await EcsRequest.AbandonAsync<SyntheticPointerEventIntent>(world, playerEntity);

            return new SyntheticPointerResult
            {
                Hit = false,
                TimedOut = true,
                FailureReason = $"the pointer gesture did not complete within {budgetSec}s (is the simulation paused?)",
                SceneEntityId = aim.EntityId ?? NO_ENTITY,
            };
        }

        private static SyntheticPointerEventIntent Intent(in PointerAim aim, InputAction button, PointerEventType eventType,
            SyntheticPressHandoff? press = null, bool force = false) =>
            new (aim.EntityId ?? NO_ENTITY, aim.SceneId, aim.AimPoint, button, eventType, press, aim.ScreenPoint, force);

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
