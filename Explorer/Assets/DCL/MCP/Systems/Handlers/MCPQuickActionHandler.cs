using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик быстрых действий (Quick Actions) - all-in-one команды
    /// </summary>
    public class MCPQuickActionHandler
    {
        private readonly World world;
        private SingleInstanceEntity? camera;
        private readonly MCPScreenshotService screenshotService;

        private SingleInstanceEntity Camera => camera ??= world.CacheCamera();

        public MCPQuickActionHandler(World world, MCPScreenshotService screenshotService)
        {
            this.world = world;
            this.screenshotService = screenshotService;
        }

        /// <summary>
        ///     Quick Screenshot of Player: открывает камеру, позиционирует вокруг игрока, делает скриншот, закрывает.
        ///     ВСЁ В ОДНОЙ КОМАНДЕ!
        /// </summary>
        public async UniTask<object> HandleQuickScreenshotOfPlayerAsync(JObject parameters)
        {
            var source = "MCP_QuickAction";

            try
            {
                // Параметры
                float distance = parameters["distance"]?.Value<float>() ?? 5f;
                float height = parameters["height"]?.Value<float>() ?? 2f;
                float yawAngle = parameters["yawAngle"]?.Value<float>() ?? 0f;
                float fov = parameters["fov"]?.Value<float>() ?? 60f;

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Quick Action] Starting quick screenshot of player: distance={distance}, height={height}, yaw={yawAngle}, fov={fov}");

                // 1. Открываем InWorld Camera
                world.Add(Camera, new ToggleInWorldCameraRequest
                {
                    IsEnable = true,
                    Source = source,
                    TargetCameraMode = null,
                });

                await UniTask.Yield(); // Ждем открытия камеры

                if (!world.Has<InWorldCameraComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "Failed to open InWorld Camera",
                    };
                }

                // 2. Включаем MCP контроль
                if (!world.Has<MCPCameraControlComponent>(Camera))
                    world.Add<MCPCameraControlComponent>(Camera);

                await UniTask.Yield();

                // 3. Получаем позицию игрока
                CameraTarget cameraTarget = world.Get<CameraTarget>(Camera);
                CharacterController followTarget = cameraTarget.Value;

                if (followTarget == null) { throw new Exception("Camera follow target is null"); }

                Transform playerTransform = followTarget.transform.parent; // Получаем transform игрока

                if (playerTransform == null)
                {
                    // Если parent null, используем сам transform followTarget
                    playerTransform = followTarget.transform;
                }

                Vector3 playerPos = playerTransform.position;
                Vector3 playerHeadPos = playerPos + (Vector3.up * 1.6f);

                // 4. Вычисляем орбитальную позицию камеры вокруг игрока
                float yawRad = yawAngle * Mathf.Deg2Rad;

                var offset = new Vector3(
                    Mathf.Sin(yawRad) * distance,
                    height,
                    Mathf.Cos(yawRad) * distance
                );

                Vector3 cameraPosition = playerHeadPos + offset;

                // 5. Устанавливаем позицию
                world.Add(Camera, new MCPCameraSetPositionCommand { TargetPosition = cameraPosition });
                await UniTask.Yield();

                // 6. Направляем на игрока
                world.Add(Camera, new MCPCameraLookAtCommand { TargetPoint = playerHeadPos });
                await UniTask.Yield();

                // 7. Устанавливаем FOV
                world.Add(Camera, new MCPCameraSetFOVCommand { FOV = fov });
                await UniTask.Yield();

                // 8. Делаем скриншот через сервис
                (bool screenshotSuccess, string thumbBase64, string screenshotError) =
                    await screenshotService.RequestScreenshotAsync(source, timeoutSeconds: 5f);

                if (!screenshotSuccess) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Quick Action] Screenshot failed: {screenshotError}"); }

                // 9. Выключаем MCP контроль и закрываем камеру
                world.Remove<MCPCameraControlComponent>(Camera);

                world.Add(Camera, new ToggleInWorldCameraRequest
                {
                    IsEnable = false,
                    Source = source,
                    TargetCameraMode = null,
                });

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Quick Action] Quick screenshot of player completed successfully");

                return new
                {
                    success = true,
                    cameraPosition = new
                    {
                        cameraPosition.x,
                        cameraPosition.y,
                        cameraPosition.z,
                    },
                    thumbnailBase64 = thumbBase64,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Quick Action] Quick screenshot failed: {e.Message}");

                // Cleanup при ошибке
                try
                {
                    if (world.Has<MCPCameraControlComponent>(Camera))
                        world.Remove<MCPCameraControlComponent>(Camera);

                    world.Add(Camera, new ToggleInWorldCameraRequest { IsEnable = false, Source = source });
                }
                catch { }

                return new
                {
                    success = false,
                    error = e.Message,
                };
            }
        }

        /// <summary>
        ///     Quick Screenshot from Point: открывает камеру, устанавливает позицию и цель, делает скриншот, закрывает.
        ///     ВСЁ В ОДНОЙ КОМАНДЕ!
        /// </summary>
        public async UniTask<object> HandleQuickScreenshotFromPointAsync(JObject parameters)
        {
            var source = "MCP_QuickAction";

            try
            {
                // Параметры
                JObject cameraPosObj = parameters["cameraPosition"]?.Value<JObject>();
                JObject targetPosObj = parameters["targetPosition"]?.Value<JObject>();
                float fov = parameters["fov"]?.Value<float>() ?? 60f;

                if (cameraPosObj == null || targetPosObj == null)
                {
                    return new
                    {
                        success = false,
                        error = "Missing cameraPosition or targetPosition parameters",
                    };
                }

                var cameraPosition = new Vector3(
                    cameraPosObj["x"]?.Value<float>() ?? 0f,
                    cameraPosObj["y"]?.Value<float>() ?? 0f,
                    cameraPosObj["z"]?.Value<float>() ?? 0f
                );

                var targetPosition = new Vector3(
                    targetPosObj["x"]?.Value<float>() ?? 0f,
                    targetPosObj["y"]?.Value<float>() ?? 0f,
                    targetPosObj["z"]?.Value<float>() ?? 0f
                );

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Quick Action] Starting quick screenshot from point: camera={cameraPosition}, target={targetPosition}, fov={fov}");

                // 1. Открываем InWorld Camera
                world.Add(Camera, new ToggleInWorldCameraRequest
                {
                    IsEnable = true,
                    Source = source,
                    TargetCameraMode = null,
                });

                await UniTask.Yield();

                if (!world.Has<InWorldCameraComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "Failed to open InWorld Camera",
                    };
                }

                // 2. Включаем MCP контроль
                if (!world.Has<MCPCameraControlComponent>(Camera))
                    world.Add<MCPCameraControlComponent>(Camera);

                await UniTask.Yield();

                // 3. Устанавливаем позицию
                world.Add(Camera, new MCPCameraSetPositionCommand { TargetPosition = cameraPosition });
                await UniTask.Yield();

                // 4. Направляем на цель
                world.Add(Camera, new MCPCameraLookAtCommand { TargetPoint = targetPosition });
                await UniTask.Yield();

                // 5. Устанавливаем FOV
                world.Add(Camera, new MCPCameraSetFOVCommand { FOV = fov });
                await UniTask.Yield();

                // 6. Делаем скриншот через сервис
                (bool screenshotSuccess, string thumbBase64, string screenshotError) =
                    await screenshotService.RequestScreenshotAsync(source, timeoutSeconds: 5f);

                if (!screenshotSuccess) { ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Quick Action] Screenshot failed: {screenshotError}"); }

                // 7. Выключаем MCP контроль и закрываем камеру
                world.Remove<MCPCameraControlComponent>(Camera);

                world.Add(Camera, new ToggleInWorldCameraRequest
                {
                    IsEnable = false,
                    Source = source,
                    TargetCameraMode = null,
                });

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Quick Action] Quick screenshot from point completed successfully");

                return new
                {
                    success = true,
                    cameraPosition = new
                    {
                        cameraPosition.x,
                        cameraPosition.y,
                        cameraPosition.z,
                    },
                    thumbnailBase64 = thumbBase64,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Quick Action] Quick screenshot failed: {e.Message}");

                // Cleanup при ошибке
                try
                {
                    if (world.Has<MCPCameraControlComponent>(Camera))
                        world.Remove<MCPCameraControlComponent>(Camera);

                    world.Add(Camera, new ToggleInWorldCameraRequest { IsEnable = false, Source = source });
                }
                catch { }

                return new
                {
                    success = false,
                    error = e.Message,
                };
            }
        }
    }
}
