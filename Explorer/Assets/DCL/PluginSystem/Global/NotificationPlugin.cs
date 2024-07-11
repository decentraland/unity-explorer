using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Notification;
using DCL.Notification.NewNotification;
using DCL.Notification.NotificationsBus;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;

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
            IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            notificationsBusController = new NotificationsBusController();
            notificationsController = new NotificationsController(webRequestController, notificationsBusController);
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(NotificationSettings notificationSettings, CancellationToken ct)
        {
            NewNotificationView newNotificationView = (await assetsProvisioner.ProvideMainAssetAsync(settings.NewNotificationView, ct: ct)).Value.GetComponent<NewNotificationView>();

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                NewNotificationController newNotificationController = new NewNotificationController(NewNotificationController.CreateLazily(newNotificationView, null), notificationsBusController);
                mvcManager.RegisterController(newNotificationController);
            };
        }


        public class NotificationSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public NewNotificationViewRef NewNotificationView { get; private set; }

            [Serializable]
            public class NewNotificationViewRef : ComponentReference<NewNotificationView>
            {
                public NewNotificationViewRef(string guid) : base(guid) { }
            }
        }
    }

}
