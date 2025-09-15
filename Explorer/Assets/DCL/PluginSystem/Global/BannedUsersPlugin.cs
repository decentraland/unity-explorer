using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
using DCL.SceneBannedUsers;
using DCL.SceneBannedUsers.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class BannedUsersPlugin : IDCLGlobalPlugin
    {
        private readonly IRoomHub roomHub;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmNavigator realmNavigator;
        private readonly ECSBannedScene bannedSceneController;

        public BannedUsersPlugin(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ECSBannedScene bannedSceneController)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.bannedSceneController = bannedSceneController;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            BannedUsersFromCurrentScene.Initialize(new BannedUsersFromCurrentScene(roomHub, selfProfile, realmNavigator, bannedSceneController));
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            BannedUsersSystem.InjectToWorld(ref builder);

        public void Dispose() { }
    }
}
