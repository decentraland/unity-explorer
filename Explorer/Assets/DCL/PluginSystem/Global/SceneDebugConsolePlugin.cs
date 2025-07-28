using Arch.SystemGroups;
using DCL.Input;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly SceneDebugConsoleLogEntryBus logLogEntriesBus;
        private IInputBlock inputBlock;
        private SceneDebugConsoleController sceneDebugConsoleController;

        public SceneDebugConsolePlugin(SceneDebugConsoleLogEntryBus logLogEntriesBus, IInputBlock inputBlock)
        {
            this.logLogEntriesBus = logLogEntriesBus;
            this.inputBlock = inputBlock;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            sceneDebugConsoleController = new SceneDebugConsoleController(logLogEntriesBus, inputBlock);
        }
    }

    /*public class SceneDebugConsolePluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public SceneDebugConsoleSettings ConsoleSettings { get; private set; }
    }*/
}
