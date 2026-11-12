using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.McpServer.Core;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using ECS;
using ECS.SceneLifeCycle;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     The locomotion recorder: plays scripted movement scenarios and records every frame as a trace (format v1,
    ///     shared with the Bevy explorer). Created only with the debug flag, and only when a <c>--locomotion-*</c>
    ///     argument asks for it or the MCP server is on (its tools start suites in a running client); a regular session
    ///     constructs nothing. Without a launch suite it stays inert until a suite is started.
    ///     <para>
    ///         Logs under <c>ReportCategory.MCP</c>: unlike <c>MOTION</c>, the production log matrix enables it at every
    ///         severity, so a player build's log shows the suites and the traces they write.
    ///     </para>
    /// </summary>
    public class LocomotionRecorderPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly LocomotionSuiteSession session;
        private readonly ILoadingStatus loadingStatus;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;

        /// <param name="launchOptions">The launch flags' suite and settings; null when the recorder exists only for the MCP tools.</param>
        public LocomotionRecorderPlugin(LocomotionRecorderOptions? launchOptions, IDebugContainerBuilder debugBuilder, ILoadingStatus loadingStatus, IScenesCache scenesCache,
            IRealmData realmData)
        {
            this.loadingStatus = loadingStatus;
            this.scenesCache = scenesCache;
            this.realmData = realmData;

            // Launched for measurements, the animations follow --locomotion-animations (off by default); created only for
            // the MCP tools, the own avatar animates as usual until a suite or set_avatar_animations says otherwise.
            session = launchOptions != null
                ? new LocomotionSuiteSession(launchOptions, launchOptions.AnimationsEnabled)
                : new LocomotionSuiteSession(new LocomotionRecorderOptions(null, 1, LocomotionRecorderOptions.DefaultOutputDirectory, null, null, false,
                    LocomotionInputSeam.Keyboard, LocomotionDeltaTime.Real, false), true);

            debugBuilder.TryAddWidget("Locomotion: Recorder")
                       ?.AddControlWithLabel("Avatar animations", new DebugToggleDef(session.AnimationsToggleBinding))
                        .AddSingleButton("Run suite", session.RequestStart)
                        .AddSingleButton("Cancel suite", () => session.Cancel())
                        .AddCustomMarker("Status:", session.Status);

            if (launchOptions?.SuitePath != null)
                session.RequestStart();
        }

        public void Dispose()
        {
            session.Dispose();
        }

        /// <summary>The MCP tools that drive this recorder; the MCP server plugin registers them.</summary>
        public McpTool[] CreateMcpTools() =>
            new McpTool[]
            {
                new RunLocomotionSuiteTool(session),
                new GetLocomotionStatusTool(session),
                new CancelLocomotionSuiteTool(session),
                new SetAvatarAnimationsTool(session),
            };

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            LocomotionSuiteSystem.InjectToWorld(ref builder, session, arguments.PlayerEntity, loadingStatus, scenesCache, realmData);
            LocomotionCameraYawSystem.InjectToWorld(ref builder, session);
            LocomotionAnimationsOffSystem.InjectToWorld(ref builder, session, arguments.PlayerEntity);
            LocomotionComponentInputSystem.InjectToWorld(ref builder, session, arguments.PlayerEntity);
        }
    }
}
