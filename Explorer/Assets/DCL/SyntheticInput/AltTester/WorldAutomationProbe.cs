#if ALTTESTER
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.SyntheticInput.Components;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     <para>
    ///         AltTester front-end of the world/avatar synthetic input: tests call these via
    ///         <c>AltDriver.CallStaticMethod</c> (assembly <c>DCL.SyntheticInput</c> — this assembly name is a
    ///         wire contract) and drive the exact same <see cref="SyntheticInputAgent" /> the MCP tools drive.
    ///     </para>
    ///     <para>
    ///         Every gesture is multi-frame, so the API is start/poll: a Start* method returns an operation id,
    ///         and <see cref="PollJson" /> reports <c>{"done":false}</c> until the payload is ready. Timeouts and
    ///         failures come back inside the payload — nothing here throws towards the test.
    ///     </para>
    /// </summary>
    public static class WorldAutomationProbe
    {
        private const float MAX_SECONDS = 30f;
        private const float DEFAULT_POINTER_TIMEOUT_SEC = 3f;

        private static SyntheticInputAgent? agent;

        /// <summary>Written once by SyntheticInputPlugin when the automation session starts (the static-latch probe pattern).</summary>
        public static void Install(SyntheticInputAgent installedAgent) =>
            agent = installedAgent;

        public static bool IsReady() =>
            agent != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        /// <summary>Walk/jog/run camera-relative for a duration; kind ∈ walk|jog|run. Scene movement locks apply unless ignoreInputModifiers.</summary>
        public static int StartWalk(float directionX, float directionY, string kind, float seconds, bool jump, bool ignoreInputModifiers)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!Enum.TryParse(kind, ignoreCase: true, out MovementKind movementKind) || movementKind == MovementKind.Idle)
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload("kind must be one of: walk, jog, run")));

            var direction = new Vector2(directionX, directionY);

            if (direction == Vector2.zero)
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload("directionX and directionY must not both be zero")));

            float clampedSeconds = Mathf.Clamp(seconds, 0.1f, MAX_SECONDS);

            return AltOperationRegistry.Start(
                readyAgent.WalkAsync(direction.normalized, movementKind, clampedSeconds, jump, ignoreInputModifiers)
                          .ContinueWith(DeliveryPayload));
        }

        /// <summary>Holds a relative camera-look (mouse-delta units per frame) for a duration.</summary>
        public static int StartCameraLook(float deltaX, float deltaY, float seconds)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(
                readyAgent.CameraLookAsync(new Vector2(deltaX, deltaY), Mathf.Clamp(seconds, 0.05f, 10f))
                          .ContinueWith(DeliveryPayload));
        }

        /// <summary>Rotates the camera to aim at a world point.</summary>
        public static int StartLookAt(float x, float y, float z)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(readyAgent.LookAtAsync(new Vector3(x, y, z)).ContinueWith(DeliveryPayload));
        }

        /// <summary>
        ///     Presses and releases a pointer button on a scene entity through the real reticle pipeline;
        ///     button ∈ pointer|primary|secondary, sceneId "" accepts the current scene.
        /// </summary>
        public static int StartClickEntity(int entityId, string sceneId, string button, float timeoutSec)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!TryParseInputAction(button, out InputAction inputAction))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload($"unknown input action '{button}'")));

            return AltOperationRegistry.Start(
                readyAgent.ClickAsync(entityId, EmptyToNull(sceneId), null, null, inputAction, ClampTimeout(timeoutSec))
                          .ContinueWith(PointerResultPayload));
        }

        /// <summary>
        ///     Clicks at a screen position in normalized image coordinates (x right 0..1, y DOWN 0..1, origin
        ///     top-left). Clicks the 3D world only: a point covered by client or scene UI fails with the cover
        ///     (reported as "blockedByUi") unless <paramref name="force" /> is set.
        /// </summary>
        public static int StartClickAtScreen(float x, float y, string button, float timeoutSec, bool force)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!TryParseInputAction(button, out InputAction inputAction))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload($"unknown input action '{button}'")));

            var screenPoint = new Vector2(x * Screen.width, (1f - y) * Screen.height);

            return AltOperationRegistry.Start(
                readyAgent.ClickAsync(-1, null, null, screenPoint, inputAction, ClampTimeout(timeoutSec), force: force)
                          .ContinueWith(PointerResultPayload));
        }

        /// <summary>
        ///     Presses a pointer button on an entity, turns the camera while it is held, then releases — the
        ///     gesture that sweeps the pointer ray a scene samples from PrimaryPointerInfo. Dragging the virtual
        ///     mouse across the world pans the camera instead, so this is the only way to drive a held sweep.
        /// </summary>
        public static int StartSweep(int entityId, string sceneId, string button, float deltaX, float deltaY, float seconds, float timeoutSec)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!TryParseInputAction(button, out InputAction inputAction))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload($"unknown input action '{button}'")));

            if (deltaX == 0f && deltaY == 0f)
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload("deltaX and deltaY must not both be zero: a sweep that does not turn the camera is a press/release pair")));

            var axisValue = new Vector2(deltaX, deltaY);

            return AltOperationRegistry.Start(
                readyAgent.SweepAsync(entityId, EmptyToNull(sceneId), null, null, inputAction, axisValue,
                               Mathf.Clamp(seconds, 0.1f, MAX_SECONDS), ClampTimeout(timeoutSec))
                          .ContinueWith(SweepResultPayload));
        }

        /// <summary>Aims at a scene entity and holds the hover (no button) for a duration.</summary>
        public static int StartHover(int entityId, float seconds)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(
                readyAgent.HoverAsync(entityId, null, null, null, Mathf.Clamp(seconds, 0.1f, MAX_SECONDS))
                          .ContinueWith(PointerResultPayload));
        }

        /// <summary>
        ///     Presses and releases an SDK input action with no aim: it reaches the scene root, because a driver
        ///     holds no cursor over a target for the reticle to follow. Use <see cref="StartGlobalInputOnEntity" />
        ///     for the entity-bound half of the fan-out.
        ///     action ∈ pointer|primary|secondary|jump|forward|backward|right|left|action3..6|walk|modifier.
        /// </summary>
        public static int StartGlobalInput(string action, float holdSeconds) =>
            StartGlobalInputOnEntity(action, holdSeconds, entityId: -1);

        /// <summary>
        ///     Presses and releases an SDK input action while the reticle is aimed at <paramref name="entityId" />,
        ///     so the scene observes it entity-bound on that target under the real qualification gates — the same
        ///     event a key pressed while looking at the entity produces.
        /// </summary>
        public static int StartGlobalInputOnEntity(string action, float holdSeconds, int entityId)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!TryParseInputAction(action, out InputAction inputAction))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload($"unknown input action '{action}'")));

            return AltOperationRegistry.Start(
                readyAgent.GlobalInputAsync(inputAction, Mathf.Clamp(holdSeconds, 0f, MAX_SECONDS), entityId)
                          .ContinueWith(PointerResultPayload));
        }

        private static bool TryGetAgent(out SyntheticInputAgent readyAgent, out int failedOperationId)
        {
            if (agent != null)
            {
                readyAgent = agent;
                failedOperationId = 0;
                return true;
            }

            readyAgent = null!;
            failedOperationId = AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload("the synthetic input layer is not installed (launch with --alttester or --mcp)")));
            return false;
        }

        private static float ClampTimeout(float timeoutSec) =>
            timeoutSec <= 0f ? DEFAULT_POINTER_TIMEOUT_SEC : Mathf.Clamp(timeoutSec, 0.5f, 15f);

        private static string? EmptyToNull(string value) =>
            string.IsNullOrEmpty(value) ? null : value;

        /// <summary>Accepts the SDK action names without their "Ia" prefix, case-insensitive, underscores ignored (e.g. "primary", "action_3").</summary>
        private static bool TryParseInputAction(string value, out InputAction action)
        {
            string normalized = value.Replace("_", "").Replace(" ", "");

            foreach (InputAction candidate in (InputAction[])Enum.GetValues(typeof(InputAction)))
            {
                string candidateName = candidate.ToString();

                if (candidateName.Length > 2 && string.Equals(candidateName[2..], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    action = candidate;
                    return true;
                }
            }

            action = default(InputAction);
            return false;
        }

        private static string DeliveryPayload(SyntheticInputDelivery delivery) =>
            new JObject
            {
                ["ok"] = delivery != SyntheticInputDelivery.TimedOut,
                ["delivery"] = delivery.ToString(),
            }.ToString();

        private static string SweepResultPayload(SyntheticSweepResult sweep)
        {
            var payload = new JObject
            {
                ["ok"] = sweep.FailureReason == null && sweep.CameraSweep == SyntheticInputDelivery.Completed,
                ["pressed"] = JObject.Parse(PointerResultPayload(sweep.Press)),
                ["sweep"] = sweep.CameraSweep.ToString(),
            };

            if (sweep.FailureReason != null)
                payload["reason"] = sweep.FailureReason;
            else
                payload["released"] = JObject.Parse(PointerResultPayload(sweep.Release));

            return payload.ToString();
        }

        private static string PointerResultPayload(SyntheticPointerResult result)
        {
            var payload = new JObject
            {
                ["ok"] = !result.TimedOut && result.FailureReason == null,
                ["hit"] = result.Hit,
                ["entityId"] = result.SceneEntityId,
                ["crdtEntityId"] = result.CrdtEntityId,
            };

            if (result.FailureReason != null)
                payload["reason"] = result.FailureReason;

            if (result.BlockedByUi != null)
                payload["blockedByUi"] = result.BlockedByUi;

            if (result.TimedOut)
                payload["timedOut"] = true;

            if (result.Hit)
            {
                payload["hitPoint"] = new JObject { ["x"] = result.HitPoint.x, ["y"] = result.HitPoint.y, ["z"] = result.HitPoint.z };
                payload["distance"] = Math.Round(result.Distance, 2);
            }

            if (result.HoverText != null)
                payload["hoverText"] = result.HoverText;

            if (result.BlockedByEntityId != null)
            {
                payload["blockedByEntityId"] = result.BlockedByEntityId;
                payload["blockedByCrdtId"] = result.BlockedByCrdtId;
                payload["blockedByCollider"] = result.BlockedByColliderName;
            }

            if (result.UpRayMissed)
                payload["upRayMissed"] = true;

            return payload.ToString();
        }
    }
}
#endif
