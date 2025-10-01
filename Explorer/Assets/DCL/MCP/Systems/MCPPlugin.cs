using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

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
    }
}
