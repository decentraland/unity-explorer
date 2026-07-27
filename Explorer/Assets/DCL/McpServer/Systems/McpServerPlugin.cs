using Arch.SystemGroups;
using CrdtEcsBridge.RestrictedActions;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.Chat.MessageBus;
using DCL.Diagnostics;
#if MCP_TEST_AUTOMATION
using DCL.FeatureFlags;
#endif
using DCL.Interaction.Utility;
using DCL.McpServer.Core;
using DCL.McpServer.Tools;
using DCL.McpServer.Utils;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.UI.DebugMenu.MessageBus;
using DCL.WebRequests.Analytics;
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
    ///     Registered only when the mcp/mcp-port app arg is present on the command line — the deep-link allowlist
    ///     drops both keys, and must keep doing so; the server binds to 127.0.0.1 exclusively and validates
    ///     browser Origins.
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
        private readonly McpNetworkLogBuffer? networkLogBuffer;
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
            McpNetworkLogBuffer? networkLogBuffer,
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
            this.networkLogBuffer = networkLogBuffer;
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

#if MCP_TEST_AUTOMATION

            // Last, once nothing can queue another press: a held key is asserted by every later keyboard event, so
            // one left down here would keep firing its action for the rest of the process.
            McpKeyboardInput.Reset();
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            McpInputOverrideSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            McpPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity);

            screenshotTool?.Dispose();
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
                          .Add(new ClickEntityTool(globalWorld, arguments.PlayerEntity));

            // Without a buffer there is no HTTP history to read, so the tool stays out of tools/list rather than
            // answering every call with an empty log.
            if (networkLogBuffer != null)
                toolsRegistry.Add(new GetNetworkLogTool(networkLogBuffer));

#if MCP_TEST_AUTOMATION

            // The client-UI automation surface: absent from release builds, where no flow drives the client's own UI.
            toolsRegistry.Add(new ListUiElementsTool())
                         .Add(new GetUiStateTool())
                         .Add(new ClickUiTool())
                         .Add(new HoverUiTool())
                         .Add(new SetUiTextTool())
                         .Add(new ScrollTool())
                         .Add(new PressKeyTool());

            // Arbitrary in-process reads, writes and invocation, so they take a second gate even here: opted into
            // with --mcp-reflection, and absent from tools/list otherwise rather than failing when called.
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.McpReflection))
                toolsRegistry.Add(new GetComponentPropertyTool())
                             .Add(new SetComponentPropertyTool())
                             .Add(new CallStaticMethodTool());
#endif

            toolsRegistry.Build();

            // Stop and release the previous instance first: an abandoned listener keeps the port bound for the
            // lifetime of the process, so the replacement would never manage to start on it.
            serverCts = serverCts.SafeRestart();
            server?.Dispose();
            server = new McpHttpServer(toolsRegistry, port);

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
