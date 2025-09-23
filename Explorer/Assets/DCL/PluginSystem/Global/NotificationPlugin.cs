using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Notifications;
using DCL.Notifications.NewNotification;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    public class NotificationPlugin : IDCLGlobalPlugin<NotificationPlugin.NotificationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly IWeb3IdentityCache web3IdentityCache;

        private CancellationTokenSource? notificationPollingCt;

        public NotificationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            NotificationsRequestController notificationsRequestController,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.notificationsRequestController = notificationsRequestController;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask InitializeAsync(NotificationSettings settings, CancellationToken ct)
        {
            web3IdentityCache.OnIdentityCleared += OnIdentityCleared;
            web3IdentityCache.OnIdentityChanged += OnIdentityChanged;

            StartPollingNotifications();

            NewNotificationView newNotificationView = (await assetsProvisioner.ProvideMainAssetAsync(settings.NewNotificationView, ct: ct)).Value.GetComponent<NewNotificationView>();
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct: ct)).Value;
            NotificationDefaultThumbnails notificationDefaultThumbnails = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationDefaultThumbnailsSO, ct: ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            NewNotificationController newNotificationController =
                new NewNotificationController(
                    NewNotificationController.CreateLazily(newNotificationView, null),
                    notificationIconTypes,
                    notificationDefaultThumbnails,
                    rarityBackgroundMapping,
                    webRequestController
                );

            mvcManager.RegisterController(newNotificationController);
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        private void OnIdentityChanged() =>
            StartPollingNotifications();

        private void OnIdentityCleared() =>
            notificationPollingCt.SafeCancelAndDispose();

        private void StartPollingNotifications()
        {
            notificationPollingCt = notificationPollingCt.SafeRestart();

            notificationsRequestController.StartGettingNewNotificationsOverTimeAsync(notificationPollingCt.Token)
                                          .SuppressCancellationThrow()
                                          .Forget();
        }

        public class NotificationSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public NewNotificationViewRef NewNotificationView { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationDefaultThumbnails> NotificationDefaultThumbnailsSO { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }

            [Serializable]
            public class NewNotificationViewRef : ComponentReference<NewNotificationView>
            {
                public NewNotificationViewRef(string guid) : base(guid) { }
            }
        }
    }
}
