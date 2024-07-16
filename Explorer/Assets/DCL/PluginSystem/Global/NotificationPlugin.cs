using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Notification;
using DCL.Notification.NewNotification;
using DCL.Notification.NotificationsBus;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class NotificationPlugin : IDCLGlobalPlugin<NotificationPlugin.NotificationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationsController notificationsController;

        public NotificationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            INotificationsBusController notificationsBusController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.notificationsBusController = notificationsBusController;
            notificationsController = new NotificationsController(webRequestController, notificationsBusController, web3IdentityCache);
        }

        public async UniTask InitializeAsync(NotificationSettings settings, CancellationToken ct)
        {
            NewNotificationView newNotificationView = (await assetsProvisioner.ProvideMainAssetAsync(settings.NewNotificationView, ct: ct)).Value.GetComponent<NewNotificationView>();
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct: ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            NewNotificationController newNotificationController =
                new NewNotificationController(
                    NewNotificationController.CreateLazily(newNotificationView, null),
                        notificationsBusController,
                        notificationIconTypes,
                        rarityBackgroundMapping,
                        webRequestController);
            mvcManager.RegisterController(newNotificationController);
        }

        public void Dispose()
        {
            notificationsController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public class NotificationSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public NewNotificationViewRef NewNotificationView { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

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
