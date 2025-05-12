using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Input;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.Commands;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using DCL.UI.MainUI;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPlugin<SceneDebugConsolePluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly SceneDebugConsoleLogHistory logHistory;
        private readonly SceneDebugConsoleLogEntryBus logLogEntriesBus;
        private readonly IInputBlock inputBlock;
        private readonly MainUIView mainUIView;
        private readonly ViewDependencies viewDependencies;
        private readonly SceneDebugConsoleCommandsBus consoleCommandsBus;

        private SceneDebugConsoleController sceneDebugConsoleController;

        public SceneDebugConsolePlugin(
            IMVCManager mvcManager,
            SceneDebugConsoleLogEntryBus logLogEntriesBus,
            SceneDebugConsoleLogHistory logHistory,
            MainUIView mainUIView,
            IInputBlock inputBlock,
            ViewDependencies viewDependencies,
            SceneDebugConsoleCommandsBus consoleCommandsBus)
        {
            this.mvcManager = mvcManager;
            this.logHistory = logHistory;
            this.logLogEntriesBus = logLogEntriesBus;
            this.mainUIView = mainUIView;
            this.inputBlock = inputBlock;
            this.viewDependencies = viewDependencies;
            this.consoleCommandsBus = consoleCommandsBus;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SceneDebugConsolePluginSettings settings, CancellationToken ct)
        {
            sceneDebugConsoleController = new SceneDebugConsoleController(
                () =>
                {
                    SceneDebugConsoleView? view = mainUIView.SceneDebugConsoleView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                logLogEntriesBus,
                logHistory,
                inputBlock,
                viewDependencies,
                consoleCommandsBus,
                settings.ConsoleSettings
            );

            mvcManager.RegisterController(sceneDebugConsoleController);

            await UniTask.CompletedTask;

            mvcManager.ShowAsync(SceneDebugConsoleController.IssueCommand(), ct);
        }
    }

    public class SceneDebugConsolePluginSettings : IDCLPluginSettings
    {
        [field: SerializeField] public SceneDebugConsoleSettings ConsoleSettings { get; private set; }
    }
}
