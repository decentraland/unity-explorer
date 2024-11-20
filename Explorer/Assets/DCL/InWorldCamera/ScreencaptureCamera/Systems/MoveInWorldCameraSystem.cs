using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.ScreencaptureCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ToggleInWorldCameraActivitySystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class MoveInWorldCameraSystem : BaseUnityLoopSystem
    {
        private const float MAX_DISTANCE_FROM_PLAYER = 16f;
        private const float TRANSLATION_SPEED = 5f;
        private const float RUN_SPEED_MULTIPLAYER = 2;

        private const float FOV_CHANGE_SPEED = 3;
        private const float FOV_DAMPING = 0.5f;
        private const float MIN_FOV = 0;
        private const float MAX_FOV = 170;

        private const float MOUSE_TRANSLATION_SPEED = 0.05f;

        private const float ROTATION_SPEED = 2;
        private const float MAX_ROTATION_PER_FRAME = 10f;
        private const float ROTATION_DAMPING = 0.1f;
        private const float MIN_VERTICAL_ANGLE = -90f;
        private const float MAX_VERTICAL_ANGLE = 90f;

        private readonly DCLInput.InWorldCameraActions inputSchema;
        private readonly Transform playerTransform;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera virtualCamera;

        public MoveInWorldCameraSystem(World world, Transform playerTransform, DCLInput.InWorldCameraActions inputSchema) : base(world)
        {
            this.playerTransform = playerTransform;
            this.inputSchema = inputSchema;
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
                var followTarget = World.Get<CameraTarget>(camera).Value;
                Translate(t, followTarget);

                if (!inputSchema.MouseDrag.IsPressed())
                    Rotate(t, followTarget.transform, ref World.Get<CameraDampedAim>(camera));

                Zoom(t, ref World.Get<CameraDampedFOV>(camera));

                cinemachinePreset.Brain.ManualUpdate(); // Update the brain manually
            }
        }

        private void Zoom(float deltaTime, ref CameraDampedFOV fov)
        {
            float zoomInput = inputSchema.Zoom.ReadValue<float>();

            if (!Mathf.Approximately(zoomInput, 0f))
                fov.Target -= zoomInput * FOV_CHANGE_SPEED * deltaTime;

            fov.Target = Mathf.Clamp(fov.Target, MIN_FOV, MAX_FOV);
            fov.Current = Mathf.SmoothDamp(fov.Current, fov.Target, ref fov.Velocity, FOV_DAMPING);

            virtualCamera.m_Lens.FieldOfView = fov.Current;
        }

        private void Rotate(float deltaTime, Transform target, ref CameraDampedAim aim)
        {
            Vector2 lookInput = inputSchema.Rotation.ReadValue<Vector2>();

            Vector2 targetRotation = lookInput * ROTATION_SPEED;
            aim.Current = Vector2.SmoothDamp(aim.Current, targetRotation, ref aim.Velocity, ROTATION_DAMPING);

            float horizontalRotation = Mathf.Clamp(aim.Current.x * deltaTime, -MAX_ROTATION_PER_FRAME, MAX_ROTATION_PER_FRAME);
            float verticalRotation = Mathf.Clamp(aim.Current.y * deltaTime, -MAX_ROTATION_PER_FRAME, MAX_ROTATION_PER_FRAME);

            target.Rotate(Vector3.up, horizontalRotation, Space.World);

            float newVerticalAngle = target.eulerAngles.x - verticalRotation;
            if (newVerticalAngle > 180f) newVerticalAngle -= 360f;
            newVerticalAngle = Mathf.Clamp(newVerticalAngle, MIN_VERTICAL_ANGLE, MAX_VERTICAL_ANGLE);

            target.localRotation = Quaternion.Euler(newVerticalAngle, target.eulerAngles.y, 0f);
        }

        private void Translate(float deltaTime, CharacterController followTarget)
        {
            Vector3 moveVector = GetMoveVectorFromInput(followTarget.transform, TRANSLATION_SPEED, deltaTime);

            if (inputSchema.MouseDrag.IsPressed())
                moveVector += GetMousePanDelta(deltaTime, followTarget);

            Vector3 restrictedMovement = RestrictedMovementBySemiSphere(playerTransform.position, followTarget.transform, moveVector, MAX_DISTANCE_FROM_PLAYER);
            followTarget.Move(restrictedMovement);
        }

        private Vector3 GetMoveVectorFromInput(Transform target, float moveSpeed, float deltaTime)
        {
            Vector2 input = inputSchema.Translation.ReadValue<Vector2>();

            Vector3 forward = target.forward.normalized * input.y;
            Vector3 horizontal = target.right.normalized * input.x;
            Vector3 vertical = target.up.normalized * inputSchema.Panning.ReadValue<float>();

            float speed = inputSchema.Run.IsPressed() ? moveSpeed * RUN_SPEED_MULTIPLAYER : moveSpeed;

            return (forward + horizontal + vertical) * (speed * deltaTime);
        }

        private Vector3 GetMousePanDelta(float deltaTime, CharacterController followTarget)
        {
            Vector2 mouseDelta = inputSchema.Rotation.ReadValue<Vector2>();

            var dragMove = new Vector3(mouseDelta.x, mouseDelta.y, 0);
            dragMove = followTarget.transform.TransformDirection(dragMove);
            return dragMove * (MOUSE_TRANSLATION_SPEED * deltaTime);
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
