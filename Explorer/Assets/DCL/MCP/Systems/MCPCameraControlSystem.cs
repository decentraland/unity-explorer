using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.InWorldCamera.Systems
{
    /// <summary>
    ///     Система для обработки MCP команд управления камерой.
    ///     Работает только когда камера активна и есть MCPCameraControlComponent.
    ///     Отключает Cinemachine Brain для предотвращения конфликтов.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(EmitInWorldCameraInputSystem))]
    [LogCategory(ReportCategory.IN_WORLD_CAMERA)]
    public partial class MCPCameraControlSystem : BaseUnityLoopSystem
    {
        private readonly Entity playerEntity;

        private SingleInstanceEntity camera;
        private ICinemachinePreset cinemachinePreset;
        private CinemachineVirtualCamera virtualCamera;
        private CinemachineBrain cinemachineBrain;

        private MCPCameraControlSystem(World world, Entity playerEntity) : base(world)
        {
            this.playerEntity = playerEntity;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
            cinemachinePreset = World.Get<ICinemachinePreset>(camera);
            virtualCamera = cinemachinePreset.InWorldCameraData.Camera;

            // Находим Cinemachine Brain на главной камере
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
                cinemachineBrain = mainCamera.GetComponent<CinemachineBrain>();
        }

        protected override void Update(float deltaTime)
        {
            // Работаем только если камера активна
            if (!World.Has<InWorldCameraComponent>(camera))
            {
                // Если камера не активна, убираем MCP контроль
                if (World.Has<MCPCameraControlComponent>(camera))
                {
                    World.Remove<MCPCameraControlComponent>(camera);
                    EnableCinemachineBrain();
                }

                return;
            }

            // Управляем Cinemachine Brain в зависимости от наличия MCP контроля
            bool hasMCPControl = World.Has<MCPCameraControlComponent>(camera);

            if (hasMCPControl)
                DisableCinemachineBrain();
            else
                EnableCinemachineBrain();

            if (!hasMCPControl) return;

            // Обрабатываем команду установки позиции
            if (World.TryGet(camera, out MCPCameraSetPositionCommand posCmd))
            {
                SetCameraPosition(posCmd.TargetPosition);
                World.Remove<MCPCameraSetPositionCommand>(camera);
            }

            // Обрабатываем команду установки rotation
            if (World.TryGet(camera, out MCPCameraSetRotationCommand rotCmd))
            {
                SetCameraRotation(rotCmd.Yaw, rotCmd.Pitch);
                World.Remove<MCPCameraSetRotationCommand>(camera);
            }

            // Обрабатываем команду lookAt точку
            if (World.TryGet(camera, out MCPCameraLookAtCommand lookAtCmd))
            {
                LookAtPoint(lookAtCmd.TargetPoint);
                World.Remove<MCPCameraLookAtCommand>(camera);
            }

            // Обрабатываем команду lookAt игрока
            if (World.Has<MCPCameraLookAtPlayerCommand>(camera))
            {
                LookAtPlayer();
                World.Remove<MCPCameraLookAtPlayerCommand>(camera);
            }

            // Обрабатываем команду установки FOV
            if (World.TryGet(camera, out MCPCameraSetFOVCommand fovCmd))
            {
                SetCameraFOV(fovCmd.FOV);
                World.Remove<MCPCameraSetFOVCommand>(camera);
            }
        }

        private void SetCameraPosition(Vector3 targetPosition)
        {
            CharacterController? followTarget = World.Get<CameraTarget>(camera).Value;

            if (followTarget == null || !followTarget.enabled)
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cannot set position - followTarget is null or disabled");
                return;
            }

            // Вычисляем разницу и перемещаем через CharacterController
            Vector3 movement = targetPosition - followTarget.transform.position;
            followTarget.Move(movement);

            ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, $"[MCP Camera] Position set to {targetPosition}");
        }

        private void SetCameraRotation(float yaw, float pitch)
        {
            CharacterController? followTarget = World.Get<CameraTarget>(camera).Value;

            if (followTarget == null)
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cannot set rotation - followTarget is null");
                return;
            }

            // Нормализуем углы
            pitch = NormalizePitch(pitch);

            // Устанавливаем rotation напрямую
            followTarget.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, $"[MCP Camera] Rotation set to yaw={yaw}, pitch={pitch}");
        }

        private void LookAtPoint(Vector3 targetPoint)
        {
            CharacterController? followTarget = World.Get<CameraTarget>(camera).Value;

            if (followTarget == null)
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cannot lookAt - followTarget is null");
                return;
            }

            Transform cameraTransform = followTarget.transform;

            // Вычисляем направление к целевой точке
            Vector3 direction = (targetPoint - cameraTransform.position).normalized;

            if (direction.sqrMagnitude < 0.001f)
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] LookAt target is too close to camera position");
                return;
            }

            // Вычисляем rotation из direction
            var targetRotation = Quaternion.LookRotation(direction);
            cameraTransform.rotation = targetRotation;

            ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, $"[MCP Camera] Looking at point {targetPoint}");
        }

        private void LookAtPlayer()
        {
            // Получаем transform игрока из ECS
            if (!World.TryGet(playerEntity, out CharacterTransform characterTransform))
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cannot lookAt player - CharacterTransform component not found");
                return;
            }

            Transform playerTransform = characterTransform.Transform;

            if (playerTransform == null)
            {
                ReportHub.LogWarning(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cannot lookAt player - playerTransform is null");
                return;
            }

            // Смотрим на центр игрока (примерно на уровне головы)
            Vector3 playerHeadPosition = playerTransform.position + (Vector3.up * 1.6f);
            LookAtPoint(playerHeadPosition);

            ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Looking at player");
        }

        private void SetCameraFOV(float fov)
        {
            // Ограничиваем по настройкам
            float clampedFOV = fov;

            virtualCamera.m_Lens.FieldOfView = clampedFOV;

            // Синхронизируем с дэмпированным FOV чтобы не было конфликта
            ref CameraDampedFOV dampedFOV = ref World.TryGetRef<CameraDampedFOV>(camera, out bool exists);

            if (exists)
            {
                dampedFOV.Current = clampedFOV;
                dampedFOV.Target = clampedFOV;
            }

            ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, $"[MCP Camera] FOV set to {clampedFOV}");
        }

        private void DisableCinemachineBrain()
        {
            if (cinemachineBrain != null && cinemachineBrain.enabled)
            {
                cinemachineBrain.enabled = false;
                ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cinemachine Brain disabled");
            }
        }

        private void EnableCinemachineBrain()
        {
            if (cinemachineBrain != null && !cinemachineBrain.enabled)
            {
                cinemachineBrain.enabled = true;
                ReportHub.Log(ReportCategory.IN_WORLD_CAMERA, "[MCP Camera] Cinemachine Brain enabled");
            }
        }

        private float NormalizePitch(float pitch)
        {
            // Нормализуем pitch в диапазон -180..180
            while (pitch > 180f) pitch -= 360f;
            while (pitch < -180f) pitch += 360f;

            return pitch;
        }
    }
}
