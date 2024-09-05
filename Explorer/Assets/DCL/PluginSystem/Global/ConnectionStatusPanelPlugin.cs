using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ConnectionStatusPanelPlugin : IDCLGlobalPlugin<ConnectionStatusPanelPlugin.ConnectionStatusPanelSettings>
    {
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;
        private readonly IRoomsStatus roomsStatus;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ECSReloadScene ecsReloadScene;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;

        public ConnectionStatusPanelPlugin(
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            IRoomsStatus roomsStatus,
            ICurrentSceneInfo currentSceneInfo,
            ECSReloadScene ecsReloadScene,
            Arch.Core.World world,
            Entity playerEntity
        )
        {
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.roomsStatus = roomsStatus;
            this.currentSceneInfo = currentSceneInfo;
            this.ecsReloadScene = ecsReloadScene;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(ConnectionStatusPanelSettings settings, CancellationToken ct)
        {
            mvcManager.RegisterController(
                new ConnectionStatusPanelController(() =>
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
                    playerEntity
                )
            );
        }

        public class ConnectionStatusPanelSettings : IDCLPluginSettings { }
    }
}
