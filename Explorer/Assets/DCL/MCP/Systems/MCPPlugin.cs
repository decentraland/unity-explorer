using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.InWorldCamera.Systems;
using DCL.MCP.Handlers;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using ECS.Abstract;
using ECS.SceneLifeCycle;
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
        private readonly IScenesCache scenesCache;
        private MCPWebSocketServer server;

        public MCPPlugin(World globalWorld, IScenesCache scenesCache)
        {
            this.globalWorld = globalWorld;
            this.scenesCache = scenesCache;
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
                var sceneInfoHandler = new MCPSceneInfoHandler(globalWorld, scenesCache);
                var textShapeHandler = new MCPTextShapeHandler();
                var meshRendererHandler = new MCPMeshRendererHandler();
                var meshColliderHandler = new MCPMeshColliderHandler();
                var transformHandler = new MCPTransformHandler();
                var sceneStaticHandler = new MCPSceneStaticHandler(scenesCache);
                var sceneCodeHandler = new MCPSceneCodeHandler(scenesCache);
                var sceneInjectionHandler = new MCPSceneInjectionHandler(scenesCache);

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

                // Регистрация обработчиков информации о сценах
                server.RegisterHandler("getAllScenesInfo", sceneInfoHandler.HandleGetAllScenesInfoAsync);
                server.RegisterHandler("getSceneInfo", sceneInfoHandler.HandleGetSceneInfoAsync);
                server.RegisterHandler("getSceneCrdtState", sceneInfoHandler.HandleGetSceneCrdtStateAsync);

                // Регистрация статического контента сцены (раздельные, минимальные ответы)
                server.RegisterHandler("getCurrentSceneStatic", sceneStaticHandler.HandleGetCurrentSceneStaticAsync); // content index + metadataRawJson
                server.RegisterHandler("getSceneStaticById", sceneStaticHandler.HandleGetSceneStaticByIdAsync); // content index + metadataRawJson
                server.RegisterHandler("getSceneContentIndex", sceneStaticHandler.HandleGetSceneContentIndexAsync); // только file/hash
                server.RegisterHandler("getSceneMetadataJson", sceneStaticHandler.HandleGetSceneMetadataJsonAsync); // только raw JSON
                server.RegisterHandler("getSceneFileUrl", sceneStaticHandler.HandleGetSceneFileUrlAsync); // резолв URL для конкретного файла
                server.RegisterHandler("getSceneMainJs", sceneCodeHandler.HandleGetSceneMainJsAsync); // исходники главного JS
                server.RegisterHandler("getSceneMainSourceMapInfo", sceneCodeHandler.HandleGetSceneMainSourceMapInfoAsync); // info about main.js.map
                server.RegisterHandler("getSceneSourceFromMap", sceneCodeHandler.HandleGetSceneSourceFromMapAsync); // extract source by name from map

                // Инжекция в onUpdate (MVP)
                server.RegisterHandler("injectSceneOnUpdate", sceneInjectionHandler.HandleInjectSceneOnUpdateAsync);
                server.RegisterHandler("debugSpawnText", sceneInjectionHandler.HandleDebugSpawnTextAsync);

                // Регистрация TextShape (создание по запросу, выполняется системой в сцене)
                server.RegisterHandler("createTextShape", textShapeHandler.HandleCreateTextShapeAsync);

                // Регистрация MeshRenderer / MeshCollider
                server.RegisterHandler("createMeshRenderer", meshRendererHandler.HandleCreateMeshRendererAsync);
                server.RegisterHandler("createMeshCollider", meshColliderHandler.HandleCreateMeshColliderAsync);

                // Регистрация Transform
                server.RegisterHandler("setEntityPosition", transformHandler.HandleSetEntityPositionAsync);

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
