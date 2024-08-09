using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.MainUI;
using ECS.SceneLifeCycle;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ConnectionStatusPanelPlugin : DCLGlobalPluginBase<ConnectionStatusPanelPlugin.ConnectionStatusPanelSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;
        private readonly IRoomsStatus roomsStatus;
        private readonly ECSReloadScene ecsReloadScene;

        public ConnectionStatusPanelPlugin(IMVCManager mvcManager, MainUIView mainUIView, IRoomsStatus roomsStatus, ECSReloadScene ecsReloadScene)
        {
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.roomsStatus = roomsStatus;
            this.ecsReloadScene = ecsReloadScene;
        }

        protected override UniTask<ContinueInitialization?> InitializeInternalAsync(ConnectionStatusPanelSettings settings, CancellationToken ct) =>
            UniTask.FromResult<ContinueInitialization?>(
                (ref ArchSystemsWorldBuilder<Arch.Core.World> _, in GlobalPluginArguments _) =>
                {
                    mvcManager.RegisterController(
                        new ConnectionStatusPanelController(() =>
                            {
                                var view = mainUIView.ConnectionStatusPanelView;
                                view!.gameObject.SetActive(true);
                                return view;
                            },
                            ecsReloadScene,
                            roomsStatus
                        )
                    );
                }
            );

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class ConnectionStatusPanelSettings : IDCLPluginSettings { }
    }
}
