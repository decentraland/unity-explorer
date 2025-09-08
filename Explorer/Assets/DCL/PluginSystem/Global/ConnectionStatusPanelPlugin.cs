using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using LiveKit.Proto;
using MVC;
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
        private readonly IRoomsStatus roomsStatus;
        private ConnectionStatusPanelController connectionStatusPanelController;
        private ConnectionStatusPanelGOController connectionStatusPanelGOController;

        public ConnectionStatusPanelPlugin(
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            IRoomsStatus roomsStatus,
            ICurrentSceneInfo currentSceneInfo,
            ECSReloadScene ecsReloadScene,
            Arch.Core.World world,
            Entity playerEntity,
            IDebugContainerBuilder debugBuilder,
            IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.currentSceneInfo = currentSceneInfo;
            this.roomsStatus = roomsStatus;

            /*connectionStatusPanelController = new ConnectionStatusPanelController(() =>
                {
                    var view = mainUIView.ConnectionStatusPanelView;
                    view!.gameObject.SetActive(true);
                    return view;
                },
                userInAppInitializationFlow,
                mvcManager,
                currentSceneInfo,
                ecsReloadScene,
                roomsStatus,
                world,
                playerEntity,
                debugBuilder
            );
            mvcManager.RegisterController(connectionStatusPanelController);*/
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public void Dispose() { }

        public async UniTask InitializeAsync(ConnectionStatusPanelSettings settings, CancellationToken ct)
        {
            connectionStatusPanelGOController = Object.Instantiate(await assetsProvisioner.ProvideMainAssetValueAsync(settings.UiDocumentPrefab, ct: ct)).GetComponent<ConnectionStatusPanelGOController>();

            OnSceneStatusUpdate(currentSceneInfo.SceneStatus.Value);
            OnSceneConnectionQualityUpdate(roomsStatus.ConnectionQualityScene.Value);
            OnIslandConnectionQualityUpdate(roomsStatus.ConnectionQualityIsland.Value);
        }

        private void OnIslandConnectionQualityUpdate(ConnectionQuality quality)
        {
            connectionStatusPanelGOController.SetGlobalRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneConnectionQualityUpdate(ConnectionQuality quality)
        {
            connectionStatusPanelGOController.SetSceneRoomStatus(GetConnectionStatus(quality));
        }

        private void OnSceneStatusUpdate(ICurrentSceneInfo.RunningStatus? status)
        {
            switch (status)
            {
                case ICurrentSceneInfo.RunningStatus.Good:
                    connectionStatusPanelGOController.SetSceneStatus(ConnectionStatus.Good);
                    break;
                case ICurrentSceneInfo.RunningStatus.Crashed:
                    connectionStatusPanelGOController.SetSceneStatus(ConnectionStatus.Lost);
                    break;
                case null:
                    connectionStatusPanelGOController.SetSceneStatus(ConnectionStatus.None);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }
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
    public class ConnectionStatusPanelSettings : IDCLPluginSettings
    {
        [field: Header(nameof(ConnectionStatusPanelPlugin) + "." + nameof(ConnectionStatusPanelSettings))]
        [field: Space]
        [field: SerializeField]
        public AssetReferenceGameObject UiDocumentPrefab;
    }
}
