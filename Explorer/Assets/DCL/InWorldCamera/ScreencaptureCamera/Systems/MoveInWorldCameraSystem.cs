using Arch.Core;
using Arch.SystemGroups;
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
            cinemachinePreset.InWorldCameraData.Camera.enabled = false;
        }

        protected override void Update(float t)
        {
            if(World.TryGet(camera, out InWorldCamera inWorldCamera))
            {
                var moveVector = GetMoveVectorFromInput(inWorldCamera.FollowTarget.transform, TRANSLATION_SPEED, t);
                var restrictedMovement = RestrictedMovementBySemiSphere(playerTransform.position, inWorldCamera.FollowTarget.transform, moveVector, MAX_DISTANCE_FROM_PLAYER);

                inWorldCamera.FollowTarget.Move(restrictedMovement);

                cinemachinePreset.Brain.ManualUpdate(); // Update the brain manually
            }
        }

        private Vector3 GetMoveVectorFromInput(Transform target, float moveSpeed, float deltaTime)
        {
            var input = inputSchema.Translation.ReadValue<Vector2>();

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

        // private static void ApplyPOV(CinemachinePOV cinemachinePOV, in CameraInput cameraInput)
        // {
        //     if (cinemachinePOV)
        //     {
        //         cinemachinePOV.m_HorizontalAxis.m_InputAxisValue = cameraInput.Delta.x;
        //         cinemachinePOV.m_VerticalAxis.m_InputAxisValue = cameraInput.Delta.y;
        //     }
        // }
    }
}
