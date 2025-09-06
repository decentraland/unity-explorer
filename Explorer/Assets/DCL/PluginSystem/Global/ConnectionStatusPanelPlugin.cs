using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.Rooms.Status;
using DCL.UI.ConnectionStatusPanel;
using DCL.UI.MainUI;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;
using MVC;

namespace DCL.PluginSystem.Global
{
    public class ConnectionStatusPanelPlugin : IDCLGlobalPluginWithoutSettings
    {
        private ConnectionStatusPanelController connectionStatusPanelController;

        public ConnectionStatusPanelPlugin(
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            IRoomsStatus roomsStatus,
            ICurrentSceneInfo currentSceneInfo,
            ECSReloadScene ecsReloadScene,
            Arch.Core.World world,
            Entity playerEntity,
            IDebugContainerBuilder debugBuilder)
        {
            connectionStatusPanelController = new ConnectionStatusPanelController(() =>
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
            mvcManager.RegisterController(connectionStatusPanelController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }
    }
}
