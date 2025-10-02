using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.InWorldCamera.Systems;
using DCL.MCP.Handlers;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using System;
using System.Threading;

namespace DCL.MCP
{
    /// <summary>
    ///     Глобальный плагин для автоматической инициализации MCP WebSocket сервера.
    ///     Запускается при старте приложения и живёт на протяжении всей сессии.
    ///     Отвечает только за инициализацию и регистрацию обработчиков команд.
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

                // Инициализация обработчиков
                var screenshotService = new MCPScreenshotService(globalWorld);
                var cameraHandler = new MCPCameraHandler(globalWorld);
                var screenshotHandler = new MCPScreenshotHandler(globalWorld);
                var quickActionHandler = new MCPQuickActionHandler(globalWorld, screenshotService);

                // Регистрация обработчиков камеры
                server.RegisterHandler("toggleInWorldCamera", cameraHandler.HandleToggleInWorldCameraAsync);
                server.RegisterHandler("startCameraControl", cameraHandler.HandleStartCameraControlAsync);
                server.RegisterHandler("stopCameraControl", cameraHandler.HandleStopCameraControlAsync);
                server.RegisterHandler("setCameraPosition", cameraHandler.HandleSetCameraPositionAsync);
                server.RegisterHandler("setCameraRotation", cameraHandler.HandleSetCameraRotationAsync);
                server.RegisterHandler("setCameraLookAt", cameraHandler.HandleSetCameraLookAtAsync);
                server.RegisterHandler("setCameraLookAtPlayer", cameraHandler.HandleSetCameraLookAtPlayerAsync);
                server.RegisterHandler("setCameraFOV", cameraHandler.HandleSetCameraFOVAsync);
                server.RegisterHandler("getCameraState", cameraHandler.HandleGetCameraStateAsync);

                // Регистрация обработчиков скриншотов
                server.RegisterHandler("takeScreenshot", screenshotHandler.HandleTakeScreenshotAsync);
                server.RegisterHandler("getLastScreenshot", screenshotHandler.HandleGetLastScreenshotAsync);
                server.RegisterHandler("downloadLastScreenshot", screenshotHandler.HandleDownloadLastScreenshotAsync);

                // Регистрация Quick Actions (⚡ Fast all-in-one)
                server.RegisterHandler("quickScreenshotOfPlayer", quickActionHandler.HandleQuickScreenshotOfPlayerAsync);
                server.RegisterHandler("quickScreenshotFromPoint", quickActionHandler.HandleQuickScreenshotFromPointAsync);

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
            MCPCameraControlSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
        }

        public void Dispose()
        {
            server?.Dispose();
            server = null;

            ReportHub.Log(ReportCategory.DEBUG, "[MCP Plugin] MCP Plugin disposed");
        }
    }
}
