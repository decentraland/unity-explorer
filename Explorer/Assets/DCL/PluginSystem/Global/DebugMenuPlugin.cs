using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Input;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.DebugMenu;
using DCL.UI.DebugMenu.LogHistory;
using DCL.UI.DebugMenu.MessageBus;
using ECS.SceneLifeCycle.CurrentScene;
using LiveKit.Proto;
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
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly IRoomsStatus roomsStatus;
        private DebugMenuController? debugMenuController;

        public DebugMenuPlugin(DebugMenuConsoleLogEntryBus consoleLogEntryBus, IInputBlock inputBlock, IAssetsProvisioner assetsProvisioner, ICurrentSceneInfo currentSceneInfo, IRoomsStatus roomsStatus)
        {
            this.logEntriesBus = consoleLogEntryBus;
            this.inputBlock = inputBlock;
            this.assetsProvisioner = assetsProvisioner;
            this.currentSceneInfo = currentSceneInfo;
            this.roomsStatus = roomsStatus;
        }

        public async UniTask InitializeAsync(DebugMenuSettings settings, CancellationToken ct)
        {
            debugMenuController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct)).GetComponent<DebugMenuController>();
            debugMenuController.SetInputBlock(inputBlock);

            currentSceneInfo.SceneStatus.OnUpdate += OnSceneStatusUpdate;
            roomsStatus.ConnectionQualityScene.OnUpdate += OnSceneConnectionQualityUpdate;
            roomsStatus.ConnectionQualityIsland.OnUpdate += OnIslandConnectionQualityUpdate;

            OnSceneStatusUpdate(currentSceneInfo.SceneStatus.Value);
            OnSceneConnectionQualityUpdate(roomsStatus.ConnectionQualityScene.Value);
            OnIslandConnectionQualityUpdate(roomsStatus.ConnectionQualityIsland.Value);
        }

        private void OnIslandConnectionQualityUpdate(ConnectionQuality quality)
        {
            debugMenuController!.SetGlobalRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneConnectionQualityUpdate(ConnectionQuality quality)
        {
            debugMenuController!.SetSceneRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneStatusUpdate(ICurrentSceneInfo.RunningStatus? status)
        {
            switch (status)
            {
                case ICurrentSceneInfo.RunningStatus.Good:
                    debugMenuController!.SetSceneStatus(ConnectionStatus.Good);
                    break;
                case ICurrentSceneInfo.RunningStatus.Crashed:
                    debugMenuController!.SetSceneStatus(ConnectionStatus.Lost);
                    break;
                case null:
                    debugMenuController!.SetSceneStatus(ConnectionStatus.None);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
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

        private static ConnectionStatus GetConnectionStatus(ConnectionQuality quality)
        {
            return quality switch
                   {
                       ConnectionQuality.QualityPoor => ConnectionStatus.Poor,
                       ConnectionQuality.QualityGood => ConnectionStatus.Good,
                       ConnectionQuality.QualityExcellent => ConnectionStatus.Excellent,
                       ConnectionQuality.QualityLost => ConnectionStatus.Lost,
                       _ => throw new ArgumentOutOfRangeException(nameof(quality), quality, null)
                   };
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
