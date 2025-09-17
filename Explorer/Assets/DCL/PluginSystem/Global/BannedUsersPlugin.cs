using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
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
        private readonly ISelfProfile selfProfile;
        private readonly ECSBannedScene bannedSceneController;
        private readonly ILoadingStatus loadingStatus;

        public BannedUsersPlugin(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            ECSBannedScene bannedSceneController,
            ILoadingStatus loadingStatus)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.bannedSceneController = bannedSceneController;
            this.loadingStatus = loadingStatus;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            BannedUsersFromCurrentScene.Initialize(new BannedUsersFromCurrentScene(roomHub, selfProfile, bannedSceneController, loadingStatus));
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            BannedUsersSystem.InjectToWorld(ref builder);

        public void Dispose() { }
    }
}
