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
    ///     AltTester front-end of the world/avatar synthetic input. The class, method and assembly
    ///     (<c>DCL.SyntheticInput</c>) names are a wire contract: AltTester resolves them by string.
    /// </summary>
    public static class WorldAutomationProbe
    {
        private const float MAX_SECONDS = 30f;
        private const float DEFAULT_POINTER_TIMEOUT_SEC = 3f;
        private const string NOT_INSTALLED = "the synthetic input layer is not installed (launch with --alttester or --mcp)";

        private static Session? session;

        public static void Install(SyntheticInputAgent installedAgent, World world, Entity playerEntity) =>
            session = new Session(installedAgent, world, playerEntity);

        public static bool IsReady() =>
            session != null;

        public static string PollJson(int operationId) =>
            AltOperationRegistry.PollJson(operationId);

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

        /// <summary>The deltas are in mouse-delta units per frame.</summary>
        public static int StartCameraLook(float deltaX, float deltaY, float seconds)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(
                readyAgent.CameraLookAsync(new Vector2(deltaX, deltaY), Mathf.Clamp(seconds, 0.05f, 10f))
                          .ContinueWith(DeliveryPayload));
        }

        public static int StartLookAt(float x, float y, float z)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(readyAgent.LookAtAsync(new Vector3(x, y, z)).ContinueWith(DeliveryPayload));
        }

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

        public static int StartHover(int entityId, float seconds)
        {
            if (!TryGetAgent(out SyntheticInputAgent readyAgent, out int failedId))
                return failedId;

            return AltOperationRegistry.Start(
                readyAgent.HoverAsync(PointerAim.AtEntity(entityId), Mathf.Clamp(seconds, 0.1f, MAX_SECONDS))
                          .ContinueWith(PointerResultPayload));
        }

        public static int StartGlobalInput(string action, float holdSeconds) =>
            StartGlobalInputOnEntity(action, holdSeconds, entityId: -1);

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

        /// <summary>Matches an <see cref="InputAction" /> name without its "Ia" prefix, ignoring case and underscores.</summary>
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
