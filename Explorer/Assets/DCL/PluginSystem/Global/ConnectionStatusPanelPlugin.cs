using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel;
using ECS.SceneLifeCycle.CurrentScene;
using Global.AppArgs;
using LiveKit.Proto;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace DCL.PluginSystem.Global
{
    public class ConnectionStatusPanelPlugin : IDCLGlobalPlugin<ConnectionStatusPanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly RoomsStatus roomsStatus;
        private readonly IAppArgs appArgs;
        private ConnectionStatusPanelController connectionStatusPanelController;

        public ConnectionStatusPanelPlugin(
            RoomsStatus roomsStatus,
            ICurrentSceneInfo currentSceneInfo,
            IAssetsProvisioner assetsProvisioner,
            IAppArgs appArgs)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.currentSceneInfo = currentSceneInfo;
            this.roomsStatus = roomsStatus;
            this.appArgs = appArgs;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose() {
            Chat.Commands.ChatCommandsBus.Instance.ConnectionStatusPanelVisibilityChanged -= EnablePanel;
        }

        public async UniTask InitializeAsync(ConnectionStatusPanelSettings settings, CancellationToken ct)
        {
            connectionStatusPanelController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct)).GetComponent<ConnectionStatusPanelController>();
            connectionStatusPanelController.gameObject.SetActive(false);

            // Subscribe to '/debug' chat command visibility toggles
            Chat.Commands.ChatCommandsBus.Instance.ConnectionStatusPanelVisibilityChanged += EnablePanel;

            if (appArgs.HasFlag(AppArgsFlags.DEBUG))
                EnablePanel(true);
        }

        private bool IsInitialized() =>
            connectionStatusPanelController.gameObject.activeSelf;

        private void InitializePanel()
        {
            connectionStatusPanelController.gameObject.SetActive(true);

            // Keep hidden until explicitly enabled
            connectionStatusPanelController.SetPanelEnabled(false);

            OnSceneStatusUpdate(currentSceneInfo.SceneStatus.Value);
            OnSceneConnectionQualityUpdate(roomsStatus.ConnectionQualityScene.Value);
            OnIslandConnectionQualityUpdate(roomsStatus.ConnectionQualityIsland.Value);
            OnAssetBundleStatusUpdate(currentSceneInfo.SceneAssetBundleStatus.Value);

            roomsStatus.ConnectionQualityScene.OnUpdate += OnSceneConnectionQualityUpdate;
            roomsStatus.ConnectionQualityIsland.OnUpdate += OnIslandConnectionQualityUpdate;
            currentSceneInfo.SceneStatus.OnUpdate += OnSceneStatusUpdate;
            currentSceneInfo.SceneAssetBundleStatus.OnUpdate += OnAssetBundleStatusUpdate;
        }

        private void EnablePanel(bool isVisible)
        {
            if (!IsInitialized())
                InitializePanel();

            connectionStatusPanelController.SetPanelEnabled(isVisible);
        }

        private void OnIslandConnectionQualityUpdate(ConnectionQuality quality) =>
            connectionStatusPanelController.SetGlobalRoomStatus(GetConnectionStatus(quality));

        private void OnSceneConnectionQualityUpdate(ConnectionQuality quality) =>
            connectionStatusPanelController.SetSceneRoomStatus(GetConnectionStatus(quality));

        private void OnSceneStatusUpdate(ICurrentSceneInfo.RunningStatus? status)
        {
            switch (status)
            {
                case ICurrentSceneInfo.RunningStatus.Good:
                    connectionStatusPanelController.SetSceneStatus(ConnectionStatus.Good);
                    break;
                case ICurrentSceneInfo.RunningStatus.Crashed:
                    connectionStatusPanelController.SetSceneStatus(ConnectionStatus.Lost);
                    break;
                case null:
                    connectionStatusPanelController.SetSceneStatus(ConnectionStatus.None);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
        }

        private void OnAssetBundleStatusUpdate(AssetBundleRegistryEnum? assetBundleStatus) =>
            connectionStatusPanelController.SetAssetBundleSceneStatus(assetBundleStatus);

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
    public class ConnectionStatusPanelSettings : IDCLPluginSettings
    {
        [field: Header(nameof(ConnectionStatusPanelPlugin) + "." + nameof(ConnectionStatusPanelSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject UiDocumentPrefab;
    }
}
