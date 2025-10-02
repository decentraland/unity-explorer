using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Обработчик команд управления камерой через MCP
    /// </summary>
    public class MCPCameraHandler
    {
        private readonly World world;
        private SingleInstanceEntity? camera;

        private SingleInstanceEntity Camera => camera ??= world.CacheCamera();

        public MCPCameraHandler(World world)
        {
            this.world = world;
        }

        /// <summary>
        ///     Переключает InWorldCamera (открывает/закрывает)
        /// </summary>
        public async UniTask<object> HandleToggleInWorldCameraAsync(JObject parameters)
        {
            bool enable = parameters["enable"]?.Value<bool>() ?? true;
            string source = parameters["source"]?.ToString() ?? "MCP";

            world.Add(Camera, new ToggleInWorldCameraRequest
            {
                IsEnable = enable,
                Source = source,
                TargetCameraMode = null,
            });

            ReportHub.Log(ReportCategory.DEBUG, $"[MCP Camera] InWorldCamera toggled: enable={enable}, source={source}");

            return new
            {
                success = true,
                enabled = enable,
                source,
            };
        }

        /// <summary>
        ///     Включает MCP контроль камеры и возвращает текущее состояние
        /// </summary>
        public async UniTask<object> HandleStartCameraControlAsync(JObject parameters)
        {
            try
            {
                // Проверяем, открыта ли InWorldCamera
                if (!world.Has<InWorldCameraComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "InWorldCamera is not active. Please open it first using toggleInWorldCamera.",
                    };
                }

                // Добавляем компонент MCP контроля
                if (!world.Has<MCPCameraControlComponent>(Camera))
                    world.Add<MCPCameraControlComponent>(Camera);

                // Получаем текущее состояние камеры
                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Camera] Camera control started");

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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] startCameraControl failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Выключает MCP контроль камеры
        /// </summary>
        public async UniTask<object> HandleStopCameraControlAsync(JObject parameters)
        {
            try
            {
                if (world.Has<MCPCameraControlComponent>(Camera))
                {
                    world.Remove<MCPCameraControlComponent>(Camera);

                    // Удаляем все незавершенные команды
                    world.Remove<MCPCameraSetPositionCommand>(Camera);
                    world.Remove<MCPCameraSetRotationCommand>(Camera);
                    world.Remove<MCPCameraLookAtCommand>(Camera);
                    world.Remove<MCPCameraLookAtPlayerCommand>(Camera);
                    world.Remove<MCPCameraSetFOVCommand>(Camera);

                    ReportHub.Log(ReportCategory.DEBUG, "[MCP Camera] Camera control stopped");
                }

                return new
                {
                    success = true,
                    message = "MCP Camera Control deactivated. User input restored.",
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] stopCameraControl failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает позицию камеры
        /// </summary>
        public async UniTask<object> HandleSetCameraPositionAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<MCPCameraControlComponent>(Camera))
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

                world.Add(Camera, new MCPCameraSetPositionCommand
                {
                    TargetPosition = new Vector3(x, y, z),
                });

                // Ждем один фрейм чтобы команда обработалась
                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Camera] Camera position set to ({x}, {y}, {z})");

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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] setCameraPosition failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает rotation камеры
        /// </summary>
        public async UniTask<object> HandleSetCameraRotationAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<MCPCameraControlComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float yaw = parameters["yaw"]?.Value<float>() ?? 0f;
                float pitch = parameters["pitch"]?.Value<float>() ?? 0f;

                world.Add(Camera, new MCPCameraSetRotationCommand
                {
                    Yaw = yaw,
                    Pitch = pitch,
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Camera] Camera rotation set to yaw={yaw}, pitch={pitch}");

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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] setCameraRotation failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Направляет камеру на указанную точку
        /// </summary>
        public async UniTask<object> HandleSetCameraLookAtAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<MCPCameraControlComponent>(Camera))
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

                world.Add(Camera, new MCPCameraLookAtCommand
                {
                    TargetPoint = new Vector3(x, y, z),
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Camera] Camera looking at ({x}, {y}, {z})");

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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] setCameraLookAt failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Направляет камеру на игрока
        /// </summary>
        public async UniTask<object> HandleSetCameraLookAtPlayerAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<MCPCameraControlComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                world.Add<MCPCameraLookAtPlayerCommand>(Camera);

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, "[MCP Camera] Camera looking at player");

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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] setCameraLookAtPlayer failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Устанавливает FOV камеры
        /// </summary>
        public async UniTask<object> HandleSetCameraFOVAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<MCPCameraControlComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "MCP Camera Control is not active. Use startCameraControl first.",
                    };
                }

                float fov = parameters["fov"]?.Value<float>() ?? 60f;

                world.Add(Camera, new MCPCameraSetFOVCommand
                {
                    FOV = fov,
                });

                await UniTask.Yield();

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                ReportHub.Log(ReportCategory.DEBUG, $"[MCP Camera] Camera FOV set to {fov}");

                return new
                {
                    success = true,
                    state.fov,
                };
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] setCameraFOV failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Возвращает текущее состояние камеры
        /// </summary>
        public async UniTask<object> HandleGetCameraStateAsync(JObject parameters)
        {
            try
            {
                if (!world.Has<InWorldCameraComponent>(Camera))
                {
                    return new
                    {
                        success = false,
                        error = "InWorldCamera is not active.",
                    };
                }

                (Vector3 position, float yaw, float pitch, float fov) state = GetCurrentCameraState();

                return new
                {
                    success = true,
                    mcpControlActive = world.Has<MCPCameraControlComponent>(Camera),
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
                ReportHub.LogError(ReportCategory.DEBUG, $"[MCP Camera] getCameraState failed: {e.Message}");
                return new { success = false, error = e.Message };
            }
        }

        /// <summary>
        ///     Helper метод для получения текущего состояния камеры
        /// </summary>
        private (Vector3 position, float yaw, float pitch, float fov) GetCurrentCameraState()
        {
            CameraTarget cameraTarget = world.Get<CameraTarget>(Camera);
            CharacterController followTarget = cameraTarget.Value;

            if (followTarget == null) { throw new Exception("Camera follow target is null"); }

            Transform transform = followTarget.transform;

            Vector3 position = transform.position;
            Vector3 eulerAngles = transform.eulerAngles;
            float yaw = eulerAngles.y;
            float pitch = eulerAngles.x;
            if (pitch > 180f) pitch -= 360f; // Normalize to -180..180

            ICinemachinePreset cinemachinePreset = world.Get<ICinemachinePreset>(Camera);
            float fov = cinemachinePreset.InWorldCameraData.Camera.m_Lens.FieldOfView;

            return (position, yaw, pitch, fov);
        }
    }
}
