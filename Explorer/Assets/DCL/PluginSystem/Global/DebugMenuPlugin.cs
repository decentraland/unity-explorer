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
    public class DebugMenuPlugin : IDCLGlobalPlugin<SceneDebugConsoleSettings>
    {
        private readonly DebugMenuLogEntryBus logEntriesBus;
        private readonly IInputBlock inputBlock;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly IRoomsStatus roomsStatus;
        private DebugMenuController? sceneDebugConsoleController;

        public DebugMenuPlugin(DebugMenuLogEntryBus logEntriesBus, IInputBlock inputBlock, IAssetsProvisioner assetsProvisioner, ICurrentSceneInfo currentSceneInfo, IRoomsStatus roomsStatus)
        {
            this.logEntriesBus = logEntriesBus;
            this.inputBlock = inputBlock;
            this.assetsProvisioner = assetsProvisioner;
            this.currentSceneInfo = currentSceneInfo;
            this.roomsStatus = roomsStatus;
        }

        public async UniTask InitializeAsync(SceneDebugConsoleSettings settings, CancellationToken ct)
        {
            sceneDebugConsoleController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct)).GetComponent<DebugMenuController>();
            sceneDebugConsoleController.SetInputBlock(inputBlock);

            currentSceneInfo.SceneStatus.OnUpdate += OnSceneStatusUpdate;
            roomsStatus.ConnectionQualityScene.OnUpdate += OnSceneConnectionQualityUpdate;
            roomsStatus.ConnectionQualityIsland.OnUpdate += OnIslandConnectionQualityUpdate;

            OnSceneStatusUpdate(currentSceneInfo.SceneStatus.Value);
            OnSceneConnectionQualityUpdate(roomsStatus.ConnectionQualityScene.Value);
            OnIslandConnectionQualityUpdate(roomsStatus.ConnectionQualityIsland.Value);
        }

        private void OnIslandConnectionQualityUpdate(ConnectionQuality quality)
        {
            sceneDebugConsoleController!.SetGlobalRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneConnectionQualityUpdate(ConnectionQuality quality)
        {
            sceneDebugConsoleController!.SetSceneRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneStatusUpdate(ICurrentSceneInfo.RunningStatus? status)
        {
            switch (status)
            {
                case ICurrentSceneInfo.RunningStatus.Good:
                    sceneDebugConsoleController!.SetSceneStatus(ConnectionStatus.Good);
                    break;
                case ICurrentSceneInfo.RunningStatus.Crashed:
                    sceneDebugConsoleController!.SetSceneStatus(ConnectionStatus.Lost);
                    break;
                case null:
                    sceneDebugConsoleController!.SetSceneStatus(ConnectionStatus.None);
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
            sceneDebugConsoleController!.PushLog(entry);
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
    public class SceneDebugConsoleSettings : IDCLPluginSettings
    {
        [field: Header(nameof(DebugMenuPlugin) + "." + nameof(SceneDebugConsoleSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject UiDocumentPrefab;
    }
}
