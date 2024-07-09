using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Notification;
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
        private NotificationsController notificationsController;

        public NotificationPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IWebRequestController webRequestController
            )
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            notificationsController = new NotificationsController(webRequestController);
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(NotificationSettings notificationSettings, CancellationToken ct)
        {
            //NotificationsSectionView notificationsSectionView = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationSection, ct)).Value;
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
            };
        }


        public class NotificationSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public NotificationSectionRef NotificationSection { get; private set; }

            [Serializable]
            public class NotificationSectionRef : ComponentReference<NotificationsSectionView>
            {
                public NotificationSectionRef(string guid) : base(guid) { }
            }
        }
    }

}
