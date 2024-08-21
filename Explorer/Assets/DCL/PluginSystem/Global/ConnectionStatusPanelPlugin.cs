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
    public class ConnectionStatusPanelPlugin : DCLGlobalPluginBase<ConnectionStatusPanelPlugin.ConnectionStatusPanelSettings>
    {
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;
        private readonly IRoomsStatus roomsStatus;
        private readonly ICurrentSceneInfo currentSceneInfo;
        private readonly ECSReloadScene ecsReloadScene;

        public ConnectionStatusPanelPlugin(
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            IRoomsStatus roomsStatus,
            ICurrentSceneInfo currentSceneInfo,
            ECSReloadScene ecsReloadScene
        )
        {
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.roomsStatus = roomsStatus;
            this.currentSceneInfo = currentSceneInfo;
            this.ecsReloadScene = ecsReloadScene;
        }

        protected override UniTask<ContinueInitialization?> InitializeInternalAsync(ConnectionStatusPanelSettings settings, CancellationToken ct) =>
            UniTask.FromResult<ContinueInitialization?>(
                (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
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
                            builder.World!,
                            arguments.PlayerEntity
                        )
                    );
                }
            );

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class ConnectionStatusPanelSettings : IDCLPluginSettings { }
    }
}
