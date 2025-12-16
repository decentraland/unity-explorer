using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.DuplicateIdentityPopup;
using DCL.Utilities.Extensions;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using LiveKit.Proto;
using LiveKit.Rooms;
using MVC;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    public class DuplicateIdentityPlugin : IDCLGlobalPlugin<DuplicateIdentityPlugin.DuplicateIdentitySettings>
    {
        private readonly IRoomHub roomHub;
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public DuplicateIdentityPlugin(
            IRoomHub roomHub,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            IWeb3Authenticator web3Authenticator,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.roomHub = roomHub;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
            this.web3Authenticator = web3Authenticator;
            this.web3IdentityCache = web3IdentityCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(DuplicateIdentitySettings settings, CancellationToken ct)
        {
            var reference = settings.DuplicateIdentityWindow.EnsureNotNull("DuplicateIdentityWindow is null in settings");

            var prefab = (await assetsProvisioner.ProvideMainAssetAsync(reference, ct)).Value;
            var duplicateIdentityViewFactory = DuplicateIdentityWindowController.CreateLazily(prefab);
            var duplicateIdentityController = new DuplicateIdentityWindowController(duplicateIdentityViewFactory, web3Authenticator, web3IdentityCache);
            mvcManager.RegisterController(duplicateIdentityController);

            roomHub.IslandRoom().ConnectionUpdated += OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
        }

        public void Dispose()
        {
            roomHub.IslandRoom().ConnectionUpdated -= OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, DisconnectReason? disconnectReason = null)
        {
            if (connectionUpdate == ConnectionUpdate.Disconnected && disconnectReason == DisconnectReason.DuplicateIdentity)
            {
                mvcManager.ShowAndForget(
                    DuplicateIdentityWindowController.IssueCommand(),
                    ct: CancellationToken.None
                );
            }
        }

        public class DuplicateIdentitySettings : IDCLPluginSettings
        {
            [field: SerializeField] public DuplicateIdentityWindowViewRef DuplicateIdentityWindow { get; private set; } = null!;

            [Serializable]
            public class DuplicateIdentityWindowViewRef : ComponentReference<DuplicateIdentityWindowView>
            {
                public DuplicateIdentityWindowViewRef(string guid) : base(guid) { }
            }
        }
    }
}

