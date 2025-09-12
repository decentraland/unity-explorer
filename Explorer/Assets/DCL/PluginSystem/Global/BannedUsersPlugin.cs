using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles.Self;
using DCL.SceneBannedUsers;
using DCL.SceneBannedUsers.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using SceneRunner.Scene;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class BannedUsersPlugin : IDCLGlobalPlugin
    {
        private readonly IRoomHub roomHub;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmNavigator realmNavigator;
        private readonly ECSReloadScene reloadScene;
        private readonly IScenesCache scenesCache;

        public BannedUsersPlugin(
            IRoomHub roomHub,
            ISelfProfile selfProfile,
            IRealmNavigator realmNavigator,
            ECSReloadScene reloadScene,
            IScenesCache scenesCache)
        {
            this.roomHub = roomHub;
            this.selfProfile = selfProfile;
            this.realmNavigator = realmNavigator;
            this.reloadScene = reloadScene;
            this.scenesCache = scenesCache;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            BannedUsersFromCurrentScene.Initialize(new BannedUsersFromCurrentScene(roomHub, selfProfile, realmNavigator, reloadScene, scenesCache));
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            BannedUsersSystem.InjectToWorld(ref builder);

        public void Dispose() { }
    }
}
