#if !NO_LIVEKIT_MODE

using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.UI.DuplicateIdentityPopup;
using DCL.Utilities.Extensions;
using LiveKit.Proto;
using LiveKit.Rooms;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using DCL.LiveKit.Public;

namespace DCL.PluginSystem.Global
{
    public class DuplicateIdentityPlugin : IDCLGlobalPlugin<DuplicateIdentityPlugin.DuplicateIdentitySettings>
    {
        private readonly IRoomHub roomHub;
        private readonly IMVCManager mvcManager;
        private readonly IAssetsProvisioner assetsProvisioner;

        private DuplicateIdentityWindowController? duplicateIdentityController;

        public DuplicateIdentityPlugin(
            IRoomHub roomHub,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner)
        {
            this.roomHub = roomHub;
            this.mvcManager = mvcManager;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(DuplicateIdentitySettings settings, CancellationToken ct)
        {
            var reference = settings.DuplicateIdentityWindow.EnsureNotNull("DuplicateIdentityWindow is null in settings");

            var prefab = (await assetsProvisioner.ProvideMainAssetAsync(reference, ct)).Value;
            var duplicateIdentityViewFactory = DuplicateIdentityWindowController.CreateLazily(prefab, null);
            duplicateIdentityController = new DuplicateIdentityWindowController(duplicateIdentityViewFactory);
            mvcManager.RegisterController(duplicateIdentityController);

            roomHub.IslandRoom().ConnectionUpdated += OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
        }

        public void Dispose()
        {
            roomHub.IslandRoom().ConnectionUpdated -= OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason = null)
        {
            if (connectionUpdate == ConnectionUpdate.Disconnected && disconnectReason == LKDisconnectReason.DuplicateIdentity && duplicateIdentityController?.State != ControllerState.ViewShowing)
                ShowDuplicateIdentityWindowAsync().Forget();

            return;

            async UniTaskVoid ShowDuplicateIdentityWindowAsync()
            {
                await UniTask.SwitchToMainThread();
                await mvcManager.ShowAsync(DuplicateIdentityWindowController.IssueCommand(), ct: CancellationToken.None);
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

#endif
