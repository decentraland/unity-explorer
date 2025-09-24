using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Input;
using DCL.UI.DebugMenu;
using DCL.UI.DebugMenu.LogHistory;
using DCL.UI.DebugMenu.MessageBus;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class DebugMenuPlugin : IDCLGlobalPlugin<DebugMenuSettings>
    {
        private readonly DebugMenuConsoleLogEntryBus logEntriesBus;
        private readonly IInputBlock inputBlock;
        private readonly IAssetsProvisioner assetsProvisioner;
        private DebugMenuController? debugMenuController;
        private readonly DebugUtilities.IDebugContainerBuilder debugContainerBuilder;

        public DebugMenuPlugin(
            DiagnosticsContainer diagnostics,
            IInputBlock inputBlock,
            IAssetsProvisioner assetsProvisioner,
            DebugUtilities.IDebugContainerBuilder debugContainerBuilder
        )
        {
            this.inputBlock = inputBlock;
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;

            logEntriesBus = new DebugMenuConsoleLogEntryBus();
            diagnostics.AddDebugConsoleHandler(logEntriesBus);
        }

        public async UniTask InitializeAsync(DebugMenuSettings settings, CancellationToken ct)
        {
            debugMenuController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct))!
                                        .GetComponent<DebugMenuController>()
                                        .EnsureNotNull(nameof(debugMenuController));

            debugMenuController.Initialize(inputBlock, debugContainerBuilder);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            logEntriesBus.MessageAdded += OnMessageAdded;
        }

        private void OnMessageAdded(DebugMenuConsoleLogEntry entry)
        {
            debugMenuController!.PushLog(entry);
        }

        public void Dispose()
        {
            // Nothing to do here
        }
    }

    [Serializable]
    public class DebugMenuSettings : IDCLPluginSettings
    {
        [field: Header(nameof(DebugMenuPlugin) + "." + nameof(DebugMenuSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject UiDocumentPrefab;
    }
}
