using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera.ScreencaptureCamera.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(EmitInWorldCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class MoveInWorldCameraSystem : BaseUnityLoopSystem
    {
        private readonly InWorldCameraMovementSettings settings;
        private readonly Transform playerTransform;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera virtualCamera;

        public MoveInWorldCameraSystem(World world, InWorldCameraMovementSettings settings, Transform playerTransform) : base(world)
        {
            this.settings = settings;
            this.playerTransform = playerTransform;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            virtualCamera = cinemachinePreset.InWorldCameraData.Camera;
        }

        protected override void Update(float t)
        {
            if (World.Has<InWorldCamera>(camera))
            {
                InWorldCameraInput input = World.Get<InWorldCameraInput>(camera);
                CharacterController? followTarget = World.Get<CameraTarget>(camera).Value;

                Translate(followTarget, input, t);

                if (!input.MouseIsDragging)
                    Rotate(ref World.Get<CameraDampedAim>(camera), followTarget.transform, input.Aim, t);

                Zoom(ref World.Get<CameraDampedFOV>(camera), input.Zoom, t);
            }
        }

        private void Zoom(ref CameraDampedFOV fov, float zoomInput, float deltaTime)
        {
            if (!Mathf.Approximately(zoomInput, 0f))
                fov.Target -= zoomInput * settings.FOVChangeSpeed * deltaTime;

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

            target.localRotation = Quaternion.Euler(newVerticalAngle, target.eulerAngles.y, 0f);
        }

        private void Translate(CharacterController followTarget, InWorldCameraInput input, float deltaTime)
        {
            Vector3 moveVector = GetMoveVectorFromInput(followTarget.transform, settings.TranslationSpeed, deltaTime, input);

            if (input.MouseIsDragging)
                moveVector += GetMousePanDelta(deltaTime, followTarget, input.Aim);

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

        private Vector3 GetMousePanDelta(float deltaTime, CharacterController followTarget, Vector2 mouseDelta)
        {
            var dragMove = new Vector3(mouseDelta.x, mouseDelta.y, 0);
            dragMove = followTarget.transform.TransformDirection(dragMove);
            return dragMove * (settings.MouseTranslationSpeed * deltaTime);
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
