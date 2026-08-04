using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.BugReporting;
using DCL.BugReporting.UI;
using DCL.Diagnostics.Sentry;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles.Self;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class BugReportPlugin : IDCLGlobalPlugin<BugReportPlugin.BugReportSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISelfProfile selfProfile;
        private readonly Arch.Core.World globalWorld;
        private readonly Entity playerEntity;

        private BugReportController? bugReportController;

        public BugReportPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISelfProfile selfProfile,
            Arch.Core.World globalWorld,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.selfProfile = selfProfile;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
        }

        public void Dispose()
        {
            bugReportController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(BugReportSettings settings, CancellationToken ct)
        {
            var bugReportViewPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.BugReportPrefab, ct))
                .Value.GetComponent<BugReportView>();

            var bugReportService = new BugReportService(
                new SentryUserFeedbackService(settings.SentryFeedbackUrlTemplate),
                new IntercomTicketClient(webRequestController, decentralandUrlsSource));

            bugReportController = new BugReportController(
                BugReportController.CreateLazily(bugReportViewPrefab, null),
                bugReportService,
                selfProfile,
                globalWorld,
                playerEntity,
                new OsFileBrowserBugReportImageProvider());

            mvcManager.RegisterController(bugReportController);
        }

        [Serializable]
        public class BugReportSettings : IDCLPluginSettings
        {
            [field: Header(nameof(BugReportPlugin) + "." + nameof(BugReportSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject BugReportPrefab { get; private set; } = null!;

            [field: Tooltip("Deep link into the Sentry feedback UI; {0} is replaced with the feedback event id")]
            [field: SerializeField]
            public string SentryFeedbackUrlTemplate { get; private set; } = "https://decentraland.sentry.io/issues/feedback/?projectSlug=unity-explorer&eventId={0}";
        }
    }
}
