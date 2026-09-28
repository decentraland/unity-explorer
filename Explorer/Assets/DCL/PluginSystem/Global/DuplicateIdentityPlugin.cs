using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Web3.Identities;
using DCL.AssetsProvision;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Connections;
using DCL.FeatureFlags;
using DCL.LiveKit.Public;
using DCL.UI.DuplicateIdentityPopup;
using DCL.Utilities.Extensions;
using DCL.Diagnostics;
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

        private DuplicateIdentityWindowController? duplicateIdentityController;
        private readonly SessionControl session;
        private readonly Func<CancellationToken, UniTask> reconnect;
        private bool disposed;
        private bool showing;

        public DuplicateIdentityPlugin(
            IRoomHub roomHub,
            IMVCManager mvcManager,
            IAssetsProvisioner assetsProvisioner,
            SessionControl session,
            Func<CancellationToken, UniTask> reconnect)
        {
            this.session = session;
            this.reconnect = reconnect;
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
            duplicateIdentityController = new DuplicateIdentityWindowController(duplicateIdentityViewFactory, session, reconnect);
            mvcManager.RegisterController(duplicateIdentityController);

            roomHub.IslandRoom().ConnectionUpdated += OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated += OnConnectionUpdated;
            session.Changed += OnSessionChanged;
            OnSessionChanged();
        }

        public void Dispose()
        {
            disposed = true;
            session.Changed -= OnSessionChanged;
            roomHub.IslandRoom().ConnectionUpdated -= OnConnectionUpdated;
            roomHub.SceneRoom().Room().ConnectionUpdated -= OnConnectionUpdated;
        }

        private void OnConnectionUpdated(IRoom room, ConnectionUpdate connectionUpdate, LKDisconnectReason? disconnectReason = null)
        {
            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.StopOnDuplicateIdentity) && connectionUpdate == ConnectionUpdate.Disconnected && disconnectReason is LKDisconnectReason.DuplicateIdentity or LKDisconnectReason.ParticipantRemoved && duplicateIdentityController?.State != ControllerState.ViewShowing)
                ShowDuplicateIdentityWindowAsync().Forget();

            return;

            async UniTaskVoid ShowDuplicateIdentityWindowAsync()
            {
                await UniTask.SwitchToMainThread();
                await mvcManager.ShowAsync(DuplicateIdentityWindowController.IssueCommand(), ct: CancellationToken.None);
            }
        }

        private void OnSessionChanged()
        {
            if (session.Current is not (SessionControl.Status.Active or SessionControl.Status.Authenticating)) ShowSessionAsync().Forget();
        }

        private async UniTaskVoid ShowSessionAsync()
        {
            await UniTask.SwitchToMainThread();
            if (disposed || showing || session.Current is SessionControl.Status.Active or SessionControl.Status.Authenticating) return;
            showing = true;
            try { await mvcManager.ShowAsync(DuplicateIdentityWindowController.IssueCommand(), ct: CancellationToken.None); }
            catch (Exception e) { ReportHub.LogException(e, ReportCategory.LIVEKIT); }
            finally { showing = false; }
        }

        [Serializable]
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

