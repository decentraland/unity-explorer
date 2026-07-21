using Arch.SystemGroups;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.UI.DebugMenu.MessageBus;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using Global.AppArgs;
using SceneRunner.Debugging.Hub;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace DCL.McpServer.Systems
{
    /// <summary>
    ///     Hosts the embedded MCP server so external coding agents can observe and drive the client.
    ///     Registered only when the mcp/mcp-port app arg is present (command line or deep link);
    ///     the server binds to 127.0.0.1 exclusively and validates browser Origins.
    /// </summary>
    public class McpServerPlugin : IDCLGlobalPluginWithoutSettings
    {
        private const int DEFAULT_PORT = 8123;

        private const int MIN_PORT = 1024;
        private const int MAX_PORT = 65535;

        private readonly int port;

        private readonly ICoroutineRunner coroutineRunner;
        private readonly ILoadingStatus loadingStatus;

        private readonly IChatMessagesBus chatMessagesBus;
        private readonly ExposedCameraData exposedCameraData;

        private readonly Arch.Core.World globalWorld;
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly IWorldInfoHub worldInfoHub;

        private readonly IScenesCache scenesCache;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ECSReloadScene reloadSceneController;
        private readonly bool localSceneDevelopment;

        private readonly SceneLogBuffer logBuffer;
        private readonly DebugMenuConsoleLogEntryBus logEntryBus;

        private McpHttpServer? server;
        private CancellationTokenSource? serverCts;

        private ScreenshotTool? screenshotTool;

        public McpServerPlugin(
            IAppArgs appArgs,
            IGlobalWorldActions globalWorldActions,
            IChatMessagesBus chatMessagesBus,
            IScenesCache scenesCache,
            ICurrentSceneInfo currentSceneInfo,
            ILoadingStatus loadingStatus,
            IWorldInfoHub worldInfoHub,
            ECSReloadScene reloadSceneController,
            DiagnosticsContainer diagnosticsContainer,
            ExposedCameraData exposedCameraData,
            IEntityCollidersGlobalCache entityCollidersGlobalCache,
            ICoroutineRunner coroutineRunner,
            Arch.Core.World globalWorld,
            bool localSceneDevelopment)
        {
            port = appArgs.TryGetValue(AppArgsFlags.MCP_PORT, out string? portValue)
                   && int.TryParse(portValue, out int parsedPort)
                   && parsedPort is >= MIN_PORT and <= MAX_PORT
                ? parsedPort
                : DEFAULT_PORT;

            this.globalWorldActions = globalWorldActions;
            this.chatMessagesBus = chatMessagesBus;
            this.scenesCache = scenesCache;
            this.currentSceneInfo = currentSceneInfo;
            this.loadingStatus = loadingStatus;
            this.worldInfoHub = worldInfoHub;
            this.reloadSceneController = reloadSceneController;
            this.exposedCameraData = exposedCameraData;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.coroutineRunner = coroutineRunner;
            this.globalWorld = globalWorld;
            this.localSceneDevelopment = localSceneDevelopment;

            logBuffer = new SceneLogBuffer();
            logEntryBus = new DebugMenuConsoleLogEntryBus();
            logEntryBus.MessageAdded += logBuffer.Append;
            diagnosticsContainer.AddDebugConsoleHandler(logEntryBus);
        }

        public void Dispose()
        {
            logEntryBus.MessageAdded -= logBuffer.Append;
            screenshotTool?.Dispose();
            server?.Dispose();
            serverCts.SafeCancelAndDispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            McpInputOverrideSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            McpPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity);

            screenshotTool = new ScreenshotTool(coroutineRunner, globalWorld, arguments.PlayerEntity);

            var toolsRegistry = new McpToolsRegistry()
                          .Add(screenshotTool)
                          .Add(new GetPlayerStateTool(globalWorld, arguments.PlayerEntity, exposedCameraData, currentSceneInfo))
                          .Add(new GetSceneStateTool(scenesCache, currentSceneInfo, loadingStatus, localSceneDevelopment))
                          .Add(new GetSceneLogsTool(logBuffer))
                          .Add(new TeleportTool(chatMessagesBus, scenesCache, loadingStatus))
                          .Add(new MoveToTool(globalWorldActions, globalWorld, arguments.PlayerEntity))
                          .Add(new LookAtTool(globalWorldActions, globalWorld, arguments.PlayerEntity, exposedCameraData))
                          .Add(new SetCameraModeTool(globalWorld, exposedCameraData))
                          .Add(new SetCameraPoseTool(globalWorld, arguments.PlayerEntity, exposedCameraData))
                          .Add(new WalkTool(globalWorld, arguments.PlayerEntity))
                          .Add(new SendChatTool(chatMessagesBus))
                          .Add(new ReloadSceneTool(reloadSceneController, scenesCache, globalWorld, arguments.PlayerEntity, arguments.SkyboxEntity))
                          .Add(new ListSceneEntitiesTool(worldInfoHub))
                          .Add(new GetEntityDetailsTool(worldInfoHub))
                          .Add(new TriggerEmoteTool(globalWorldActions))
                          .Add(new ClickEntityTool(globalWorld, arguments.PlayerEntity))
                          .Build();

            server = new McpHttpServer(toolsRegistry, port);
            serverCts = serverCts.SafeRestart();

            bool started = server.TryStart();

            if (started)
                server.RunAsync(serverCts.Token).Forget();

            AnnounceStatusWhenLoadedAsync(started, server.EndpointUrl, serverCts.Token).Forget();
        }

        /// <summary>
        ///     Reports the server address (or the startup failure) once loading completes, so the message
        ///     reaches the scene debug console: its UI subscribes to log entries only after this plugin runs,
        ///     and a line logged at server start would be dropped.
        /// </summary>
        private async UniTaskVoid AnnounceStatusWhenLoadedAsync(bool started, string endpointUrl, CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);

                if (started)
                    ReportHub.Log(LogType.Log, ReportCategory.MCP, $"MCP server listening on {endpointUrl}");
                else
                    ReportHub.LogError(ReportCategory.MCP, $"MCP server failed to start on port {port} — agent connections unavailable (pass a different --mcp-port)");
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(LogType.Log, ReportCategory.MCP, "MCP server status announcement cancelled before loading completed");
            }
        }
    }
}
