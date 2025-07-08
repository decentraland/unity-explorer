using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPlugin<SceneDebugConsolePluginSettings>
    {
        private readonly SceneDebugConsoleLogHistory logHistory;
        private readonly SceneDebugConsoleLogEntryBus logLogEntriesBus;
        private readonly IInputBlock inputBlock;
        private readonly SceneDebugConsoleCommandsBus consoleCommandsBus;

        private SceneDebugConsoleController sceneDebugConsoleController;

        public SceneDebugConsolePlugin(
            SceneDebugConsoleLogEntryBus logLogEntriesBus,
            SceneDebugConsoleLogHistory logHistory,
            IInputBlock inputBlock,
            SceneDebugConsoleCommandsBus consoleCommandsBus)
        {
            this.logHistory = logHistory;
            this.logLogEntriesBus = logLogEntriesBus;
            this.inputBlock = inputBlock;
            this.consoleCommandsBus = consoleCommandsBus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SceneDebugConsolePluginSettings settings, CancellationToken ct)
        {
            sceneDebugConsoleController = new SceneDebugConsoleController(
                logLogEntriesBus,
                logHistory,
                inputBlock,
                consoleCommandsBus,
                settings.ConsoleSettings
            );

            await UniTask.CompletedTask;
        }
    }

    public class SceneDebugConsolePluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public SceneDebugConsoleSettings ConsoleSettings { get; private set; }
    }
}
