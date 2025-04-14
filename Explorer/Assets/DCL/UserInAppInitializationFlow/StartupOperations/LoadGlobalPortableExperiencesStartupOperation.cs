using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.RealmNavigation;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Global.Dynamic.DebugSettings;
using PortableExperiences.Controller;
using System.Collections.Generic;
using System.Threading;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class LoadGlobalPortableExperiencesStartupOperation : StartUpOperationBase
    {
        private readonly ILoadingStatus loadingStatus;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IDebugSettings debugSettings;
        private readonly IPortableExperiencesController portableExperiencesController;

        public LoadGlobalPortableExperiencesStartupOperation(
            ILoadingStatus loadingStatus,
            FeatureFlagsCache featureFlagsCache,
            IDebugSettings debugSettings,
            IPortableExperiencesController portableExperiencesController)
        {
            this.loadingStatus = loadingStatus;
            this.featureFlagsCache = featureFlagsCache;
            this.debugSettings = debugSettings;
            this.portableExperiencesController = portableExperiencesController;
        }

        protected override UniTask InternalExecuteAsync(IStartupOperation.Params args, CancellationToken ct)
        {
            float finalizationProgress = loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.GlobalPXsLoading);
            LoadDebugPortableExperiences(ct);
            LoadRemotePortableExperiences(ct);
            args.Report.SetProgress(finalizationProgress);
            return UniTask.CompletedTask;
        }

        private void LoadRemotePortableExperiences(CancellationToken ct)
        {
            //This allows us to disable loading the global px on debug builds or when using the editor in case we wanted to load a cleaner experience.
            if (!debugSettings.EnableRemotePortableExperiences) return;

            if (featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE, FeatureFlagsStrings.CSV_VARIANT))
            {
                if (!featureFlagsCache.Configuration.TryGetCsvPayload(FeatureFlagsStrings.GLOBAL_PORTABLE_EXPERIENCE, FeatureFlagsStrings.CSV_VARIANT, out List<List<string>>? csv)) return;

                if (csv?[0] == null) return;

                foreach (string value in csv[0]) { portableExperiencesController.
                    CreatePortableExperienceByEnsAsync(new ENS(value), ct, true, true).
                    SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE).Forget(); }
            }
        }

        private void LoadDebugPortableExperiences(CancellationToken ct)
        {
            if (debugSettings.PortableExperiencesEnsToLoad == null) return;

            foreach (string pxEns in debugSettings.PortableExperiencesEnsToLoad)
            {
                portableExperiencesController.
                    CreatePortableExperienceByEnsAsync(new ENS(pxEns), ct, true, true).
                    SuppressAnyExceptionWithFallback(new IPortableExperiencesController.SpawnResponse(), ReportCategory.PORTABLE_EXPERIENCE).Forget();
            }
        }
    }
}
