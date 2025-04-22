using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.InWorldCamera.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(EmitInWorldCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class MoveInWorldCameraSystem : BaseUnityLoopSystem
    {
        private const float ZOOM_SPEED_SCALAR_WINDOWS = 20f;
        private const float ZOOM_SPEED_SCALAR_MAC = 1f;

        private readonly float zoomSpeedScalar;
        private readonly InWorldCameraMovementSettings settings;
        private readonly Transform playerTransform;
        private readonly ICursor cursor;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera virtualCamera;
        private bool cursorWasLocked;

        private MoveInWorldCameraSystem(World world, InWorldCameraMovementSettings settings, Transform playerTransform, ICursor cursor) : base(world)
        {
            this.settings = settings;
            this.playerTransform = playerTransform;
            this.cursor = cursor;

            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
                zoomSpeedScalar = ZOOM_SPEED_SCALAR_MAC;
            else
                zoomSpeedScalar = ZOOM_SPEED_SCALAR_WINDOWS;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            virtualCamera = cinemachinePreset.InWorldCameraData.Camera;
        }

        protected override void Update(float t)
        {
            if (World.Has<InWorldCameraComponent>(camera))
            {
                InWorldCameraInput input = World.Get<InWorldCameraInput>(camera);
                CharacterController? followTarget = World.Get<CameraTarget>(camera).Value;

                Translate(followTarget, input, t);

                bool cursorIsLocked = cursor.IsLocked();

                if (cursorIsLocked || input.MouseIsDragging)
                    Rotate(ref World.Get<CameraDampedAim>(camera), followTarget.transform, input.Aim, t);

                Tilt(ref World.Get<CameraDampedTilt>(camera), followTarget.transform, input.Tilting, resetTilt: cursorIsLocked != cursorWasLocked, t);

                Zoom(ref World.Get<CameraDampedFOV>(camera), input.Zoom, t);
            }
        }

        private void Zoom(ref CameraDampedFOV fov, float zoomInput, float deltaTime)
        {
            if (!Mathf.Approximately(zoomInput, 0f))
                fov.Target -= zoomInput * settings.FOVChangeSpeed * deltaTime * zoomSpeedScalar;

            fov.Target = Mathf.Clamp(fov.Target, settings.MinFOV, settings.MaxFOV);
            fov.Current = Mathf.SmoothDamp(fov.Current, fov.Target, ref fov.Velocity, settings.FOVDamping);

            virtualCamera.m_Lens.FieldOfView = fov.Current;
        }

        private void Rotate(ref CameraDampedAim aim, Transform target, Vector2 lookInput, float deltaTime)
        {
            Vector2 targetRotation = lookInput * settings.RotationSpeed;
            aim.Current = Vector2.SmoothDamp(aim.Current, targetRotation, ref aim.Velocity, settings.RotationDamping);

            float horizontalRotation = Mathf.Clamp(aim.Current.x * deltaTime, -settings.MaxRotationPerFrame, settings.MaxRotationPerFrame);
            float verticalRotation = Mathf.Clamp(aim.Current.y * deltaTime, -settings.MaxRotationPerFrame, settings.MaxRotationPerFrame);

            target.Rotate(Vector3.up, horizontalRotation, Space.World);

            float newVerticalAngle = target.eulerAngles.x - verticalRotation;
            if (newVerticalAngle > 180f) newVerticalAngle -= 360f;
            newVerticalAngle = Mathf.Clamp(newVerticalAngle, settings.MinVerticalAngle, settings.MaxVerticalAngle);

            target.localRotation = Quaternion.Euler(newVerticalAngle, target.eulerAngles.y, target.eulerAngles.z);
        }

        private void Tilt(ref CameraDampedTilt tilt, Transform target, float tiltInput, bool resetTilt, float deltaTime)
        {
            if (resetTilt)
            {
                tilt.Current = 0;
                tilt.Target = 0;
                tilt.Velocity = 0;

                target.transform.localRotation = Quaternion.Euler(target.transform.eulerAngles.x, target.transform.eulerAngles.y, 0f);

                cursorWasLocked = !cursorWasLocked;
            }

            if (!Mathf.Approximately(tiltInput, 0f))
            {
                float targetRotation = -tiltInput * settings.TiltSpeed;
                tilt.Target = targetRotation;
            }
            else
                tilt.Target = 0f;

            tilt.Current = Mathf.SmoothDamp(tilt.Current, tilt.Target, ref tilt.Velocity, settings.TiltDamping);

            float tiltAmount = Mathf.Clamp(tilt.Current * deltaTime, -settings.MaxTiltPerFrame, settings.MaxTiltPerFrame);

            float currentRoll = target.eulerAngles.z;
            if (currentRoll > 180f) currentRoll -= 360f;

            float newRoll = currentRoll + tiltAmount;
            newRoll = Mathf.Clamp(newRoll, -settings.MaxTiltAngle, settings.MaxTiltAngle);

            target.localRotation = Quaternion.Euler(target.eulerAngles.x, target.eulerAngles.y, newRoll);
        }

        private void Translate(CharacterController followTarget, InWorldCameraInput input, float deltaTime)
        {
            if (!followTarget.enabled) return;

            Vector3 moveVector = GetMoveVectorFromInput(followTarget.transform, settings.TranslationSpeed, deltaTime, input);
            Vector3 restrictedMovement = RestrictedMovementBySemiSphere(playerTransform.position, followTarget.transform, moveVector, settings.MaxDistanceFromPlayer);

            followTarget.Move(restrictedMovement);
        }

        private Vector3 GetMoveVectorFromInput(Transform target, float moveSpeed, float deltaTime, InWorldCameraInput input)
        {
            Vector3 forward = target.forward.normalized * input.Translation.y;
            Vector3 horizontal = target.right.normalized * input.Translation.x;
            Vector3 vertical = target.up.normalized * input.Panning;

            float speed = input.IsRunning ? moveSpeed * settings.RunSpeedMultiplayer : moveSpeed;
            return (forward + horizontal + vertical) * (speed * deltaTime);
        }

        private static Vector3 RestrictedMovementBySemiSphere(Vector3 playerPosition, Transform target, Vector3 movementVector, float maxDistanceFromPlayer)
        {
            if (target.position.y + movementVector.y <= 0f)
                movementVector.y = 0f;

            Vector3 desiredCameraPosition = target.position + movementVector;

            float distanceFromPlayer = Vector3.Distance(desiredCameraPosition, playerPosition);

            // If the distance is greater than the allowed radius, correct the movement vector
            if (distanceFromPlayer > maxDistanceFromPlayer)
            {
                Vector3 directionFromPlayer = (desiredCameraPosition - playerPosition).normalized;
                desiredCameraPosition = playerPosition + (directionFromPlayer * maxDistanceFromPlayer);
                movementVector = desiredCameraPosition - target.position;
            }

            return movementVector;
        }
    }
}
