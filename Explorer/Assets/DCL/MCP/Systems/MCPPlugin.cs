using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.MCP
{
    /// <summary>
    ///     Глобальный плагин для автоматической инициализации MCP WebSocket сервера.
    ///     Запускается при старте приложения и живёт на протяжении всей сессии.
    /// </summary>
    public class MCPPlugin : IDCLGlobalPluginWithoutSettings
    {
        private const int DEFAULT_PORT = 7777;

        private readonly World globalWorld;
        private MCPWebSocketServer server;

        public MCPPlugin(World globalWorld)
        {
            this.globalWorld = globalWorld;
        }

        public async UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            try
            {
                server = new MCPWebSocketServer();

                // Регистрация обработчиков для InWorldCamera
                server.RegisterHandler("toggleInWorldCamera", HandleToggleInWorldCamera);
                server.RegisterHandler("takeScreenshot", HandleTakeScreenshot);
                server.RegisterHandler("getLastScreenshot", HandleGetLastScreenshot);
                server.RegisterHandler("downloadLastScreenshot", HandleDownloadLastScreenshot);

                // Регистрация обработчиков для MCP Camera Control
                server.RegisterHandler("startCameraControl", HandleStartCameraControl);
                server.RegisterHandler("stopCameraControl", HandleStopCameraControl);
                server.RegisterHandler("setCameraPosition", HandleSetCameraPosition);
                server.RegisterHandler("setCameraRotation", HandleSetCameraRotation);
                server.RegisterHandler("setCameraLookAt", HandleSetCameraLookAt);
                server.RegisterHandler("setCameraLookAtPlayer", HandleSetCameraLookAtPlayer);
                server.RegisterHandler("setCameraFOV", HandleSetCameraFOV);
                server.RegisterHandler("getCameraState", HandleGetCameraState);

                server.Start();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] MCP WebSocket Server successfully started on port {DEFAULT_PORT}");
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] Failed to start MCP Server: {e}");
                throw;
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // MCP Server не требует инъекции систем в ECS World
        }

        public void Dispose()
        {
            server?.Dispose();
            server = null;

            ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] MCP Plugin disposed");
        }

        /// <summary>
        ///     Переключает InWorldCamera (открывает/закрывает)
        /// </summary>
        private async UniTask<object> HandleToggleInWorldCamera(JObject parameters)
        {
            bool enable = parameters["enable"]?.Value<bool>() ?? true;
            string source = parameters["source"]?.ToString() ?? "MCP";

            SingleInstanceEntity camera = globalWorld.CacheCamera();

            globalWorld.Add(camera, new ToggleInWorldCameraRequest
            {
                IsEnable = enable,
                Source = source,
                TargetCameraMode = null,
            });

            ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] InWorldCamera toggled: enable={enable}, source={source}");

            return new
            {
                success = true,
                enabled = enable,
                source,
            };
        }

        /// <summary>
        ///     Делает скриншот (InWorldCamera должна быть открыта)
        /// </summary>
        private async UniTask<object> HandleTakeScreenshot(JObject parameters)
        {
            string source = parameters["source"]?.ToString() ?? "MCP";

            SingleInstanceEntity camera = globalWorld.CacheCamera();

            // Проверяем, открыта ли InWorldCamera
            if (!globalWorld.Has<InWorldCameraComponent>(camera))
            {
                return new
                {
                    success = false,
                    error = "InWorldCamera is not active. Please open it first using toggleInWorldCamera.",
                };
            }

            globalWorld.Add(camera, new TakeScreenshotRequest { Source = source });

            ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] Screenshot requested, source={source}");

            return new
            {
                success = true,
                source,
            };
        }

        /// <summary>
        ///     Возвращает последний скриншот в виде base64 PNG (если есть)
        /// </summary>
        private async UniTask<object> HandleGetLastScreenshot(JObject parameters)
        {
            try
            {
                if (!MCPScreenshotStorage.HasScreenshot())
                {
                    return new
                    {
                        success = false,
                        error = "No screenshot available",
                    };
                }

                string base64 = MCPScreenshotStorage.GetLastScreenshotBase64();
                string thumbBase64 = MCPScreenshotStorage.GetLastThumbnailBase64();
                string source = MCPScreenshotStorage.GetLastSource();

                return new
                {
                    success = true,
                    imageBase64 = base64,
                    thumbnailBase64 = thumbBase64,
                    mimeType = "image/png",
                    source,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] getLastScreenshot failed: {e.Message}");

                return new
                {
                    success = false,
                    error = e.Message,
                };
            }
        }

        /// <summary>
        ///     Возвращает подсказку где скачать последний скриншот (без передачи содержимого),
        ///     чтобы MCP мог скачать файл отдельно (например, через REST, если будет добавлено) или
        ///     просто вернуть base64 миниатюру, а полный — отдельным инструментом загрузки
        /// </summary>
        private async UniTask<object> HandleDownloadLastScreenshot(JObject parameters)
        {
            if (!MCPScreenshotStorage.HasScreenshot())
            {
                return new
                {
                    success = false,
                    error = "No screenshot available",
                };
            }

            // Пока REST-скачивание не реализовано, возвращаем только то, что есть: миниатюру и информацию,
            // а MCP tool сможет сохранить base64 на диск
            string base64 = MCPScreenshotStorage.GetLastScreenshotBase64();
            string source = MCPScreenshotStorage.GetLastSource();

            return new
            {
                success = true,
                imageBase64 = base64,
                mimeType = "image/png",
                source,
                note = "For large files, consider saving base64 to disk on the MCP side.",
            };
        }

        // ============================================
        // MCP Camera Control Handlers
        // ============================================

        /// <summary>
        ///     Включает MCP контроль камеры и возвращает текущее состояние
        /// </summary>
        private async UniTask<object> HandleStartCameraControl(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                // Проверяем, открыта ли InWorldCamera
                if (!globalWorld.Has<InWorldCameraComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "InWorldCamera is not active. Please open it first using toggleInWorldCamera.",
                    };
                }

                // Добавляем компонент MCP контроля
                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                    globalWorld.Add<MCPCameraControlComponent>(camera);

                // Получаем текущее состояние камеры
                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] Camera control started");

                return new
                {
                    success = true,
                    message = "MCP Camera Control activated. User input disabled.",
                    position = new
                    {
                        state.position.x,
                        state.position.y,
                        state.position.z,
                    },
                    rotation = new
                    {
                        state.yaw,
                        state.pitch,
                    },
                    state.fov,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] startCameraControl failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Выключает MCP контроль камеры
        /// </summary>
        private async UniTask<object> HandleStopCameraControl(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    globalWorld.Remove<MCPCameraControlComponent>(camera);

                    // Удаляем все незавершенные команды
                    globalWorld.Remove<MCPCameraSetPositionCommand>(camera);
                    globalWorld.Remove<MCPCameraSetRotationCommand>(camera);
                    globalWorld.Remove<MCPCameraLookAtCommand>(camera);
                    globalWorld.Remove<MCPCameraLookAtPlayerCommand>(camera);
                    globalWorld.Remove<MCPCameraSetFOVCommand>(camera);

                    ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] Camera control stopped");
                }

                return new
                {
                    success = true,
                    message = "MCP Camera Control deactivated. User input restored.",
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] stopCameraControl failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает позицию камеры
        /// </summary>
        private async UniTask<object> HandleSetCameraPosition(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float x = parameters["x"]?.Value<float>() ?? 0f;
                float y = parameters["y"]?.Value<float>() ?? 0f;
                float z = parameters["z"]?.Value<float>() ?? 0f;

                globalWorld.Add(camera, new MCPCameraSetPositionCommand
                {
                    TargetPosition = new Vector3(x, y, z),
                });

                // Ждем один фрейм чтобы команда обработалась
                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] Camera position set to ({x}, {y}, {z})");

                return new
                {
                    success = true,
                    position = new
                    {
                        state.position.x,
                        state.position.y,
                        state.position.z,
                    },
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] setCameraPosition failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает rotation камеры
        /// </summary>
        private async UniTask<object> HandleSetCameraRotation(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float yaw = parameters["yaw"]?.Value<float>() ?? 0f;
                float pitch = parameters["pitch"]?.Value<float>() ?? 0f;

                globalWorld.Add(camera, new MCPCameraSetRotationCommand
                {
                    Yaw = yaw,
                    Pitch = pitch,
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] Camera rotation set to yaw={yaw}, pitch={pitch}");

                return new
                {
                    success = true,
                    rotation = new
                    {
                        state.yaw,
                        state.pitch,
                    },
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] setCameraRotation failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Направляет камеру на указанную точку
        /// </summary>
        private async UniTask<object> HandleSetCameraLookAt(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float x = parameters["x"]?.Value<float>() ?? 0f;
                float y = parameters["y"]?.Value<float>() ?? 0f;
                float z = parameters["z"]?.Value<float>() ?? 0f;

                globalWorld.Add(camera, new MCPCameraLookAtCommand
                {
                    TargetPoint = new Vector3(x, y, z),
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] Camera looking at ({x}, {y}, {z})");

                return new
                {
                    success = true,
                    lookingAt = new { x, y, z },
                    rotation = new
                    {
                        state.yaw,
                        state.pitch,
                    },
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] setCameraLookAt failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Направляет камеру на игрока
        /// </summary>
        private async UniTask<object> HandleSetCameraLookAtPlayer(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                globalWorld.Add<MCPCameraLookAtPlayerCommand>(camera);

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] Camera looking at player");

                return new
                {
                    success = true,
                    message = "Camera is now looking at player",
                    rotation = new
                    {
                        state.yaw,
                        state.pitch,
                    },
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] setCameraLookAtPlayer failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает FOV камеры
        /// </summary>
        private async UniTask<object> HandleSetCameraFOV(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<MCPCameraControlComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float fov = parameters["fov"]?.Value<float>() ?? 60f;

                globalWorld.Add(camera, new MCPCameraSetFOVCommand
                {
                    FOV = fov,
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Plugin] Camera FOV set to {fov}");

                return new
                {
                    success = true,
                    state.fov,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] setCameraFOV failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Возвращает текущее состояние камеры
        /// </summary>
        private async UniTask<object> HandleGetCameraState(JObject parameters)
        {
            try
            {
                SingleInstanceEntity camera = globalWorld.CacheCamera();

                if (!globalWorld.Has<InWorldCameraComponent>(camera))
                {
                    return new
                    {
                        success = false,
                        error = "InWorldCamera is not active.",
                    };
                }

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState(camera);

                return new
                {
                    success = true,
                    mcpControlActive = globalWorld.Has<MCPCameraControlComponent>(camera),
                    position = new
                    {
                        state.position.x,
                        state.position.y,
                        state.position.z,
                    },
                    rotation = new
                    {
                        state.yaw,
                        state.pitch,
                    },
                    state.fov,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Plugin] getCameraState failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Helper метод для получения текущего состояния камеры
        /// </summary>
        private (Vector3 position, float yaw, float pitch, float fov) GetCurrentCameraState(SingleInstanceEntity camera)
        {
            CameraTarget cameraTarget = globalWorld.Get<CameraTarget>(camera);
            CharacterController followTarget = cameraTarget.Value;
            Transform transform = followTarget.transform;

            Vector3 position = transform.position;
            Vector3 eulerAngles = transform.eulerAngles;
            float yaw = eulerAngles.y;
            float pitch = eulerAngles.x;
            if (pitch > 180f) pitch -= 360f; // Normalize to -180..180

            ICinemachinePreset cinemachinePreset = globalWorld.Get<ICinemachinePreset>(camera);
            float fov = cinemachinePreset.InWorldCameraData.Camera.m_Lens.FieldOfView;

            return (position, yaw, pitch, fov);
        }
    }
}
