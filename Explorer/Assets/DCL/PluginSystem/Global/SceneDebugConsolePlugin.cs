using Arch.SystemGroups;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly SceneDebugConsoleLogHistory logHistory;
        private readonly SceneDebugConsoleLogEntryBus logLogEntriesBus;
        private SceneDebugConsoleController sceneDebugConsoleController;

        public SceneDebugConsolePlugin(
            SceneDebugConsoleLogEntryBus logLogEntriesBus,
            SceneDebugConsoleLogHistory logHistory)
        {
            this.logHistory = logHistory;
            this.logLogEntriesBus = logLogEntriesBus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            sceneDebugConsoleController = new SceneDebugConsoleController(
                logLogEntriesBus,
                logHistory
            );
        }
    }

    /*public class SceneDebugConsolePluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public SceneDebugConsoleSettings ConsoleSettings { get; private set; }
    }*/
}
