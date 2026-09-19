#if ALTTESTER
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.ECSComponents;
using DCL.SyntheticInput.Components;
using DCL.SyntheticInput.UiSimulation;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;
using Utility;

namespace DCL.SyntheticInput.AltTester
{
    /// <summary>
    ///     AltTester front-end of the world/avatar synthetic input: tests call these via
    ///     <c>AltDriver.CallStaticMethod</c> (assembly <c>DCL.SyntheticInput</c> — the assembly name is a wire
    ///     contract) and drive the same <see cref="SyntheticInputAgent" /> the MCP tools drive. Every gesture is
    ///     multi-frame, so the API is start/poll: a Start* method returns an operation id, and
    ///     <see cref="PollJson" /> reports <c>{"done":false}</c> until the payload is ready. Timeouts and failures
    ///     come back inside the payload — nothing here throws towards the test.
    /// </summary>
    public static class WorldAutomationProbe
    {
        private const float MAX_SECONDS = 30f;
        private const float DEFAULT_POINTER_TIMEOUT_SEC = 3f;
        private const string NOT_INSTALLED = "the synthetic input layer is not installed (launch with --alttester or --mcp)";

        private static Session? session;

        /// <summary>Written once, when the automation session starts.</summary>
        public static void Install(SyntheticInputAgent installedAgent, World world, Entity playerEntity) =>
            session = new Session(installedAgent, world, playerEntity);

        public static bool IsReady() =>
            session != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

        /// <summary>
        ///     The player's pose right now, in one round-trip:
        ///     <c>{"ok":true,"position":{x,y,z},"rotationEuler":{x,y,z},"parcel":{x,y},"velocity":{x,y,z},"isGrounded":true}</c>.
        /// </summary>
        public static string GetPlayerStateJson()
        {
            if (session == null)
                return AltOperationRegistry.ErrorPayload(NOT_INSTALLED);

            CharacterTransform characterTransform = session.World.Get<CharacterTransform>(session.PlayerEntity);
            session.World.TryGet(session.PlayerEntity, out CharacterRigidTransform? rigidTransform);

            return new JObject
            {
                ["ok"] = true,
                ["position"] = VectorJson(characterTransform.Position),
                ["rotationEuler"] = VectorJson(characterTransform.Rotation.eulerAngles),
                ["parcel"] = ParcelJson(characterTransform.Position.ToParcel()),
                ["velocity"] = VectorJson(rigidTransform?.MoveVelocity.Velocity ?? Vector3.zero),
                ["isGrounded"] = rigidTransform?.IsGrounded ?? false,
            }.ToString();
        }

        /// <summary>
        ///     Walk/jog/run camera-relative for a duration; kind ∈ walk|jog|run. Scene movement locks apply unless
        ///     ignoreInputModifiers. The payload carries the start and end positions and the distance covered.
        /// </summary>
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
                WalkPayloadAsync(readyAgent.WalkAsync(direction.normalized, movementKind, clampedSeconds, jump, ignoreInputModifiers)));
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
                readyAgent.ClickAsync(PointerAim.AtEntity(entityId, EmptyToNull(sceneId)), inputAction, ClampTimeout(timeoutSec))
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

            var aim = PointerAim.AtScreenPoint(UiScreenGeometry.NormalizedImageToScreenPoint(new Vector2(x, y)));

            return AltOperationRegistry.Start(
                readyAgent.ClickAsync(aim, inputAction, ClampTimeout(timeoutSec), force: force)
                          .ContinueWith(PointerResultPayload));
        }

        /// <summary>
        ///     Presses a pointer button on an entity, turns the camera while it is held, then releases — the gesture
        ///     that sweeps the pointer ray a scene samples from PrimaryPointerInfo. Dragging the virtual mouse across
        ///     the world pans the camera instead, so this is the only way to drive a held sweep. The target has to be
        ///     on screen for the pointer to be parked at all, so aim the camera at it first.
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
                readyAgent.SweepAsync(PointerAim.AtEntity(entityId, EmptyToNull(sceneId)), inputAction, axisValue,
                               Mathf.Clamp(seconds, 0.1f, MAX_SECONDS), ClampTimeout(timeoutSec))
                          .ContinueWith(SweepResultPayload));
        }

        /// <summary>Aims at a scene entity and holds the hover (no button) for a duration.</summary>
        public static int StartHover(int entityId, float seconds)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(
                readyAgent.HoverAsync(PointerAim.AtEntity(entityId), Mathf.Clamp(seconds, 0.1f, MAX_SECONDS))
                          .ContinueWith(PointerResultPayload));
        }

        /// <summary>
        ///     Presses and releases an SDK input action with no aim, so it reaches the scene root; see
        ///     <see cref="StartGlobalInputOnEntity" /> for the entity-bound half of the fan-out.
        ///     action ∈ pointer|primary|secondary|jump|forward|backward|right|left|action3..6|walk|modifier.
        /// </summary>
        public static int StartGlobalInput(string action, float holdSeconds) =>
            StartGlobalInputOnEntity(action, holdSeconds, entityId: -1);

        /// <summary>
        ///     Presses and releases an SDK input action while the reticle is aimed at <paramref name="entityId" />,
        ///     so the scene observes it entity-bound under the real qualification gates.
        /// </summary>
        public static int StartGlobalInputOnEntity(string action, float holdSeconds, int entityId)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            if (!TryParseInputAction(action, out InputAction inputAction))
                return AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload($"unknown input action '{action}'")));

            PointerAim aim = entityId >= 0 ? PointerAim.AtEntity(entityId) : PointerAim.None;

            return AltOperationRegistry.Start(
                readyAgent.GlobalInputAsync(inputAction, Mathf.Clamp(holdSeconds, 0f, MAX_SECONDS), aim)
                          .ContinueWith(PointerResultPayload));
        }

        private static bool TryGetAgent(out SyntheticInputAgent readyAgent, out int failedOperationId)
        {
            if (session != null)
            {
                readyAgent = session.Agent;
                failedOperationId = 0;
                return true;
            }

            readyAgent = null!;
            failedOperationId = AltOperationRegistry.Start(UniTask.FromResult(AltOperationRegistry.ErrorPayload(NOT_INSTALLED)));
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

        /// <summary>The delivery payload plus where the hold took the player, mirroring the MCP walk tool.</summary>
        private static async UniTask<string> WalkPayloadAsync(UniTask<SyntheticInputDelivery> walk)
        {
            Vector3 startPosition = ReadPlayerPosition();
            SyntheticInputDelivery delivery = await walk;
            await UniTask.SwitchToMainThread();
            Vector3 endPosition = ReadPlayerPosition();

            return new JObject
            {
                ["ok"] = delivery != SyntheticInputDelivery.TimedOut,
                ["delivery"] = delivery.ToString(),
                ["startPosition"] = VectorJson(startPosition),
                ["endPosition"] = VectorJson(endPosition),
                ["distance"] = Math.Round(Vector3.Distance(startPosition, endPosition), 2),
                ["parcel"] = ParcelJson(endPosition.ToParcel()),
            }.ToString();
        }

        /// <summary>Only reached through a Start* method, which already proved the session is installed.</summary>
        private static Vector3 ReadPlayerPosition() =>
            session == null ? Vector3.zero : session.World.Get<CharacterTransform>(session.PlayerEntity).Position;

        private static JObject VectorJson(Vector3 value) =>
            new () { ["x"] = Math.Round(value.x, 3), ["y"] = Math.Round(value.y, 3), ["z"] = Math.Round(value.z, 3) };

        private static JObject ParcelJson(Vector2Int parcel) =>
            new () { ["x"] = parcel.x, ["y"] = parcel.y };

        private static string SweepResultPayload(SyntheticSweepResult sweep)
        {
            var payload = new JObject
            {
                ["ok"] = sweep.FailureReason == null && sweep.CameraSweep == SyntheticInputDelivery.Completed,
                ["pressed"] = PointerResultJson(in sweep.Press),
                ["sweep"] = sweep.CameraSweep.ToString(),
            };

            if (sweep.FailureReason != null)
                payload["reason"] = sweep.FailureReason;
            else
                payload["released"] = PointerResultJson(in sweep.Release);

            return payload.ToString();
        }

        private static string PointerResultPayload(SyntheticPointerResult result) =>
            PointerResultJson(in result).ToString();

        /// <summary>The pointer-result shape shared with the MCP tools, plus the probe's ok flag.</summary>
        private static JObject PointerResultJson(in SyntheticPointerResult result)
        {
            JObject payload = result.ToJson();
            payload["ok"] = !result.TimedOut && result.FailureReason == null;
            return payload;
        }

        private sealed class Session
        {
            public readonly SyntheticInputAgent Agent;
            public readonly World World;
            public readonly Entity PlayerEntity;

            public Session(SyntheticInputAgent agent, World world, Entity playerEntity)
            {
                Agent = agent;
                World = world;
                PlayerEntity = playerEntity;
            }
        }
    }
}
#endif
