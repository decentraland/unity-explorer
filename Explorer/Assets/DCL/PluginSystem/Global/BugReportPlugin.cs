using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.BugReporting;
using DCL.BugReporting.UI;
using DCL.Diagnostics.Sentry;
using DCL.Input;
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
        private readonly IInputBlock inputBlock;
        private readonly Arch.Core.World globalWorld;
        private readonly Entity playerEntity;
        private readonly IBugReportSessionContext sessionContext;

        private BugReportController? bugReportController;
        private PerformanceIssuePromptController? performanceIssuePromptController;
        private PerformanceIssueDetector? performanceIssueDetector;

        public BugReportPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISelfProfile selfProfile,
            IInputBlock inputBlock,
            Arch.Core.World globalWorld,
            Entity playerEntity,
            IBugReportSessionContext sessionContext)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.selfProfile = selfProfile;
            this.inputBlock = inputBlock;
            this.globalWorld = globalWorld;
            this.playerEntity = playerEntity;
            this.sessionContext = sessionContext;
        }

        public void Dispose()
        {
            bugReportController?.Dispose();
            performanceIssuePromptController?.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // The detector exists only when InitializeAsync found the prompt prefab configured.
            if (performanceIssueDetector != null)
                PerformanceIssuePromptSystem.InjectToWorld(ref builder, mvcManager, performanceIssueDetector);
        }

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
                inputBlock,
                globalWorld,
                playerEntity,
                new OsFileBrowserBugReportImageProvider(),
                sessionContext);

            mvcManager.RegisterController(bugReportController);

            // The prompt ships behind the prefab assignment: with none configured the form stays
            // reachable through its manual entry points only.
            if (settings.PerformanceIssuePromptPrefab.RuntimeKeyIsValid())
            {
                var promptViewPrefab = (await assetsProvisioner.ProvideMainAssetAsync(settings.PerformanceIssuePromptPrefab, ct))
                    .Value.GetComponent<PerformanceIssuePromptView>();

                performanceIssuePromptController = new PerformanceIssuePromptController(
                    PerformanceIssuePromptController.CreateLazily(promptViewPrefab, null),
                    mvcManager);

                mvcManager.RegisterController(performanceIssuePromptController);

                performanceIssueDetector = new PerformanceIssueDetector(
                    settings.PromptHiccupSeconds,
                    settings.PromptLowFpsThreshold,
                    settings.PromptLowFpsWindowSeconds);
            }
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

            [field: Tooltip("Popup offered when a performance drop is detected. Leave unassigned to disable the prompt")]
            [field: SerializeField]
            public AssetReferenceGameObject PerformanceIssuePromptPrefab { get; private set; } = null!;

            [field: Tooltip("A single frame at least this long (seconds) counts as a performance issue")]
            [field: SerializeField]
            public float PromptHiccupSeconds { get; private set; } = 1f;

            [field: Tooltip("An average FPS below this over the measuring window counts as a performance issue")]
            [field: SerializeField]
            public float PromptLowFpsThreshold { get; private set; } = 15f;

            [field: Tooltip("Length (seconds) of the rolling window the average FPS is measured over")]
            [field: SerializeField]
            public float PromptLowFpsWindowSeconds { get; private set; } = 10f;
        }
    }
}
