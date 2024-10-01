using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using Global.Dynamic;
using Global.Dynamic.DebugSettings;
using PortableExperiences.Controller;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class LoadGlobalPortableExperiencesStartupOperation : IStartupOperation
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly ISelfProfile selfProfile;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly IDebugSettings debugSettings;
        private readonly IPortableExperiencesController portableExperiencesController;

        public LoadGlobalPortableExperiencesStartupOperation(
            RealFlowLoadingStatus loadingStatus,
            ISelfProfile selfProfile,
            FeatureFlagsCache featureFlagsCache,
            IDebugSettings debugSettings,
            IPortableExperiencesController portableExperiencesController)
        {
            this.loadingStatus = loadingStatus;
            this.selfProfile = selfProfile;
            this.featureFlagsCache = featureFlagsCache;
            this.debugSettings = debugSettings;
            this.portableExperiencesController = portableExperiencesController;
        }

        public async UniTask<Result> ExecuteAsync(AsyncLoadProcessReport report, CancellationToken ct)
        {
            await CheckGlobalPxLoadingConditionsAsync(ct);

            report.SetProgress(loadingStatus.SetStage(RealFlowLoadingStatus.Stage.GlobalPXsLoaded));
            return Result.SuccessResult();
        }

        private async UniTask CheckGlobalPxLoadingConditionsAsync(CancellationToken ct)
        {
            var ownProfile = await selfProfile.ProfileAsync(ct);

            //If we havent completed the tutorial, we won't load the GlobalPX as it will interfere with the onboarding.
            if (ownProfile is not { TutorialStep: > 0 })
                return;

            LoadDebugPortableExperiences(ct);
            LoadRemotePortableExperiences(ct);
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
