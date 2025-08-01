using Arch.SystemGroups;
using DCL.Input;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using UnityEngine;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly SceneDebugConsoleLogEntryBus logLogEntriesBus;
        private readonly IInputBlock inputBlock;
        private SceneDebugConsoleController? sceneDebugConsoleController;

        public SceneDebugConsolePlugin(SceneDebugConsoleLogEntryBus logLogEntriesBus, IInputBlock inputBlock)
        {
            this.logLogEntriesBus = logLogEntriesBus;
            this.inputBlock = inputBlock;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // TODO: move this reference to PluginSettings ?
            sceneDebugConsoleController = Object.Instantiate(Resources.Load<SceneDebugConsoleController>("SceneDebugConsoleRootCanvas"));
            sceneDebugConsoleController.SetInputBlock(inputBlock);

            logLogEntriesBus.MessageAdded += OnMessageAdded;
        }

        private void OnMessageAdded(SceneDebugConsoleLogEntry entry)
        {
            sceneDebugConsoleController?.PushLog(entry);
        }

        public void Dispose()
        {
            logLogEntriesBus.MessageAdded -= OnMessageAdded;
            UnityObjectUtils.SafeDestroyGameObject(sceneDebugConsoleController);
        }
    }
}
