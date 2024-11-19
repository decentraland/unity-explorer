using Arch.Core;
using Arch.SystemGroups;
using Cinemachine.Utility;
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

        private readonly Transform playerTransform;
        private readonly DCLInput.InWorldCameraActions inputSchema;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;

        public MoveInWorldCameraSystem(World world, Transform playerTransform, DCLInput.InWorldCameraActions inputSchema) : base(world)
        {
            this.playerTransform = playerTransform;
            this.inputSchema = inputSchema;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            cinemachinePreset = World.Get<ICinemachinePreset>(camera);

        }

        protected override void Update(float t)
        {
            if (World.TryGet(camera, out InWorldCamera inWorldCamera))
            {
                // Translate(t, inWorldCamera.FollowTarget);
                // Rotate(t, inWorldCamera.FollowTarget.transform);

                HandleFreeCameraMovement(cinemachinePreset, t);

                cinemachinePreset.Brain.ManualUpdate(); // Update the brain manually
            }
        }

        private void HandleFreeCameraMovement(ICinemachinePreset cinemachinePreset, float deltaTime)
        {
            var virtualCamera = cinemachinePreset.InWorldCameraData.Camera;
            var transform = virtualCamera.transform;
            Vector2 input = inputSchema.Translation.ReadValue<Vector2>();

            Vector3 moveDir = new Vector3(input.x, 0, input.y);
            moveDir = transform.TransformDirection(moveDir);
            moveDir.y = inputSchema.Panning.ReadValue<float>();
            // float speed = input.Sprint ? moveSpeed * 2 : moveSpeed; // Optional: sprint modifier

            axis += Damper.Damp(moveDir - axis, 1f, deltaTime);

            transform.position += axis * (TRANSLATION_SPEED * deltaTime);
        }

        private Vector3 axis;

        private Vector2 currentRotation;
        private Vector2 rotationVelocity;

        private float rotationSpeed = 2;
        private float maxRotationPerFrame = 10f;
        private float rotationDamping =  0.1f;
        private float minVerticalAngle = -90f;
        private float maxVerticalAngle = 90f;

        private void Rotate(float deltaTime, Transform target )
        {
            var lookInput = inputSchema.Rotation.ReadValue<Vector2>();

            Vector2 targetRotation = lookInput * rotationSpeed;
            currentRotation = Vector2.SmoothDamp(currentRotation, targetRotation, ref rotationVelocity, rotationDamping);

            float horizontalRotation = Mathf.Clamp(currentRotation.x * deltaTime, -maxRotationPerFrame, maxRotationPerFrame);
            float verticalRotation = Mathf.Clamp(currentRotation.y * deltaTime, -maxRotationPerFrame, maxRotationPerFrame);

            target.Rotate(Vector3.up, horizontalRotation, Space.World);

            float newVerticalAngle = target.eulerAngles.x - verticalRotation;
            if (newVerticalAngle > 180f) newVerticalAngle -= 360f;
            newVerticalAngle = Mathf.Clamp(newVerticalAngle, minVerticalAngle, maxVerticalAngle);

            target.localRotation = Quaternion.Euler(newVerticalAngle, target.eulerAngles.y, 0f);
        }

        private void Translate(float deltaTime, CharacterController followTarget)
        {
            Vector3 moveVector = GetMoveVectorFromInput(followTarget.transform, TRANSLATION_SPEED, deltaTime);
            Vector3 restrictedMovement = RestrictedMovementBySemiSphere(playerTransform.position, followTarget.transform, moveVector, MAX_DISTANCE_FROM_PLAYER);
            followTarget.Move(restrictedMovement);
        }

        private Vector3 GetMoveVectorFromInput(Transform target, float moveSpeed, float deltaTime)
        {
            Vector2 input = inputSchema.Translation.ReadValue<Vector2>();

            Vector3 forward = target.forward.normalized * input.y;
            Vector3 horizontal = target.right.normalized * input.x;
            Vector3 vertical = target.up.normalized * inputSchema.Panning.ReadValue<float>();

            return (forward + horizontal + vertical) * (moveSpeed * deltaTime);
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
