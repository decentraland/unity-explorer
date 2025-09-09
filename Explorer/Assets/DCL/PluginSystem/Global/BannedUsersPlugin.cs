using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.SceneBannedUsers;
using DCL.SceneBannedUsers.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class BannedUsersPlugin : IDCLGlobalPlugin<BannedUsersPlugin.BannedUsersSettings>
    {
        private readonly IScenesCache scenesCache;
        private readonly IRoomHub roomHub;
        private readonly IRealmNavigator realmNavigator;
        private readonly Vector2Int startParcelInGenesis;

        public BannedUsersPlugin(
            IScenesCache scenesCache,
            IRoomHub roomHub,
            IRealmNavigator realmNavigator,
            Vector2Int startParcelInGenesis)
        {
            this.scenesCache = scenesCache;
            this.roomHub = roomHub;
            this.realmNavigator = realmNavigator;
            this.startParcelInGenesis = startParcelInGenesis;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            BannedUsersSystem.InjectToWorld(ref builder, realmNavigator, startParcelInGenesis);

        public async UniTask InitializeAsync(BannedUsersSettings settings, CancellationToken ct) =>
            BannedUsersFromCurrentScene.Initialize(new BannedUsersFromCurrentScene(scenesCache, roomHub, settings.CurrentSceneBannedWallets));

        public void Dispose() { }

        [Serializable]
        public class BannedUsersSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public CurrentSceneBannedWalletsConfiguration CurrentSceneBannedWallets { get; private set; }
        }
    }
}
