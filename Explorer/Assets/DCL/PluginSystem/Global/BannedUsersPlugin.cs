using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using DCL.SceneBannedUsers;
using DCL.SceneBannedUsers.Systems;
using ECS.SceneLifeCycle;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class BannedUsersPlugin : IDCLGlobalPlugin
    {
        private readonly IRoomHub roomHub;
        private readonly ECSBannedScene bannedSceneController;
        private readonly ILoadingStatus loadingStatus;
        private readonly bool includeBannedUsersFromScene;

        private PlayerBannedScenesController playerBannedScenesController;

        public BannedUsersPlugin(
            IRoomHub roomHub,
            ECSBannedScene bannedSceneController,
            ILoadingStatus loadingStatus,
            bool includeBannedUsersFromScene)
        {
            this.roomHub = roomHub;
            this.bannedSceneController = bannedSceneController;
            this.loadingStatus = loadingStatus;
            this.includeBannedUsersFromScene = includeBannedUsersFromScene;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            BannedUsersFromCurrentScene.Initialize(new BannedUsersFromCurrentScene(roomHub, includeBannedUsersFromScene));

            if (includeBannedUsersFromScene)
                playerBannedScenesController = new PlayerBannedScenesController(roomHub, bannedSceneController, loadingStatus);

            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            if (!includeBannedUsersFromScene)
                return;

            BannedUsersSystem.InjectToWorld(ref builder);
        }

        public void Dispose() =>
            playerBannedScenesController.Dispose();
    }
}
