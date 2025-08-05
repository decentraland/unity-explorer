using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.UI.SceneDebugConsole;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class SceneDebugConsolePlugin : IDCLGlobalPlugin<SceneDebugConsoleSettings>
    {
        private readonly SceneDebugConsoleLogEntryBus logEntriesBus;
        private readonly IInputBlock inputBlock;
        private readonly IAssetsProvisioner assetsProvisioner;
        private SceneDebugConsoleController? sceneDebugConsoleController;

        public SceneDebugConsolePlugin(SceneDebugConsoleLogEntryBus logEntriesBus, IInputBlock inputBlock, IAssetsProvisioner assetsProvisioner)
        {
            this.logEntriesBus = logEntriesBus;
            this.inputBlock = inputBlock;
            this.assetsProvisioner = assetsProvisioner;
        }

        public async UniTask InitializeAsync(SceneDebugConsoleSettings settings, CancellationToken ct)
        {
            sceneDebugConsoleController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct)).GetComponent<SceneDebugConsoleController>();
            // sceneDebugConsoleController.Initialize();
            sceneDebugConsoleController.SetInputBlock(inputBlock);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            logEntriesBus.MessageAdded += OnMessageAdded;
        }

        private void OnMessageAdded(SceneDebugConsoleLogEntry entry)
        {
            sceneDebugConsoleController?.PushLog(entry);
        }

        public void Dispose()
        {
            logEntriesBus.MessageAdded -= OnMessageAdded;
            UnityObjectUtils.SafeDestroyGameObject(sceneDebugConsoleController);
        }
    }

    [Serializable]
    public class SceneDebugConsoleSettings : IDCLPluginSettings
    {
        [field: Header(nameof(SceneDebugConsolePlugin) + "." + nameof(SceneDebugConsoleSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject UiDocumentPrefab;
    }
}
