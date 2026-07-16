using Arch.SystemGroups;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
using DCL.Interaction.Utility;
using DCL.Mcp.Protocol;
using DCL.Mcp.Systems;
using DCL.Mcp.Tools;
using DCL.Mcp.Transport;
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

namespace DCL.Mcp
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
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly IChatMessagesBus chatMessagesBus;
        private readonly IScenesCache scenesCache;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ILoadingStatus loadingStatus;
        private readonly IWorldInfoHub worldInfoHub;
        private readonly ECSReloadScene reloadSceneController;
        private readonly ExposedCameraData exposedCameraData;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly Arch.Core.World globalWorld;
        private readonly bool localSceneDevelopment;
        private readonly SceneLogBuffer logBuffer;

        private ScreenshotTool? screenshotTool;
        private McpHttpServer? server;
        private CancellationTokenSource? serverCts;

        public McpServerPlugin(
            int port,
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
            this.port = port;
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
            var logEntryBus = new DebugMenuConsoleLogEntryBus();
            logEntryBus.MessageAdded += logBuffer.Append;
            diagnosticsContainer.AddDebugConsoleHandler(logEntryBus);
        }

        public void Dispose()
        {
            screenshotTool?.Dispose();
            server?.Dispose();
            serverCts.SafeCancelAndDispose();
        }

        public static bool IsEnabled(IAppArgs appArgs) =>
            appArgs.HasFlag(AppArgsFlags.MCP) || appArgs.HasFlag(AppArgsFlags.MCP_PORT);

        public static int ResolvePort(IAppArgs appArgs)
        {
            if (appArgs.TryGetValue(AppArgsFlags.MCP_PORT, out string? portValue)
                && int.TryParse(portValue, out int parsedPort)
                && parsedPort is >= MIN_PORT and <= MAX_PORT)
                return parsedPort;

            return DEFAULT_PORT;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            McpInputOverrideSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            McpPointerClickSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity);

            var registry = new McpToolRegistry();

            screenshotTool = new ScreenshotTool(coroutineRunner, globalWorld, arguments.PlayerEntity);
            registry.Register(screenshotTool);
            registry.Register(new GetPlayerStateTool(globalWorld, arguments.PlayerEntity, exposedCameraData, currentSceneInfo));
            registry.Register(new GetSceneStateTool(scenesCache, currentSceneInfo, loadingStatus, localSceneDevelopment));
            registry.Register(new GetSceneLogsTool(logBuffer));
            registry.Register(new TeleportTool(chatMessagesBus, scenesCache, loadingStatus));
            registry.Register(new MoveToTool(globalWorldActions, globalWorld, arguments.PlayerEntity));
            registry.Register(new LookAtTool(globalWorldActions, globalWorld, arguments.PlayerEntity, exposedCameraData));
            registry.Register(new SetCameraModeTool(globalWorld, exposedCameraData));
            registry.Register(new SetCameraPoseTool(globalWorld, arguments.PlayerEntity, exposedCameraData));
            registry.Register(new WalkTool(globalWorld, arguments.PlayerEntity));
            registry.Register(new ReloadSceneTool(reloadSceneController, scenesCache, globalWorld, arguments.PlayerEntity, arguments.SkyboxEntity));
            registry.Register(new ListSceneEntitiesTool(worldInfoHub));
            registry.Register(new GetEntityDetailsTool(worldInfoHub));
            registry.Register(new ClickEntityTool(globalWorld, arguments.PlayerEntity));

            var dispatcher = new McpJsonRpcDispatcher(registry, Application.version);

            server = new McpHttpServer(dispatcher, port);
            serverCts = serverCts.SafeRestart();

            bool started = server.TryStart();

            if (started)
                server.RunAsync(serverCts.Token).Forget();

            AnnounceStatusWhenLoadedAsync(started, serverCts.Token).Forget();
        }

        /// <summary>
        ///     Reports the server address (or the startup failure) once loading completes, so the message
        ///     reaches the scene debug console: its UI subscribes to log entries only after this plugin runs,
        ///     and a line logged at server start would be dropped.
        /// </summary>
        private async UniTaskVoid AnnounceStatusWhenLoadedAsync(bool started, CancellationToken ct)
        {
            try
            {
                await UniTask.WaitUntil(() => loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed, cancellationToken: ct);

                if (started)
                    ReportHub.Log(LogType.Log, ReportCategory.MCP, $"MCP server listening on http://127.0.0.1:{port}/mcp");
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
