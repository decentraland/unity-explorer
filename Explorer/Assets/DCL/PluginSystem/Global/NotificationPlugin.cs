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
    public class NotificationPlugin : DCLGlobalPluginBase<NotificationPlugin.NotificationSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly INotificationsBusController notificationsBusController;
        private NotificationsController notificationsController;

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

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(NotificationSettings notificationSettings, CancellationToken ct)
        {
            NewNotificationView newNotificationView = (await assetsProvisioner.ProvideMainAssetAsync(settings.NewNotificationView, ct: ct)).Value.GetComponent<NewNotificationView>();
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct: ct)).Value;

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                NewNotificationController newNotificationController =
                    new NewNotificationController(
                        NewNotificationController.CreateLazily(newNotificationView, null),
                        notificationsBusController,
                        notificationIconTypes,
                        webRequestController);
                mvcManager.RegisterController(newNotificationController);
            };
        }

        public override void Dispose()
        {
            notificationsController.Dispose();
        }

        public class NotificationSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public NewNotificationViewRef NewNotificationView { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

            [Serializable]
            public class NewNotificationViewRef : ComponentReference<NewNotificationView>
            {
                public NewNotificationViewRef(string guid) : base(guid) { }
            }
        }
    }

}
