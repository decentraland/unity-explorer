using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Profiles.Self;
using DCL.Utilities;
using MVC;
using System.Threading;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.HealthChecks;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.UserInAppInitializationFlow.StartupOperations.Struct;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global.AppArgs;
using UnityEngine;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInAppInitializationFlow : IUserInAppInitializationFlow
    {
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly AudioClipConfig backgroundMusic;
        private readonly ILoadingScreen loadingScreen;
        private readonly ISelfProfile selfProfile;

        private readonly LoadPlayerAvatarStartupOperation loadPlayerAvatarStartupOperation;
        private readonly CheckOnboardingStartupOperation checkOnboardingStartupOperation;
        private readonly RestartRealmStartupOperation restartRealmStartupOperation;

        private readonly IStartupOperation startupOperation;

        private static readonly ILoadingScreen.EmptyLoadingScreen EMPTY_LOADING_SCREEN = new ();

        public RealUserInAppInitializationFlow(
            RealFlowLoadingStatus loadingStatus,
            IHealthCheck livekitHealthCheck,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IFeatureFlagsProvider featureFlagsProvider,
            FeatureFlagsCache featureFlagsCache,
            IWeb3IdentityCache web3IdentityCache,
            IRealmController realmController,
            IAppArgs appParameters
        )
        {
            this.loadingStatus = loadingStatus;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.loadingScreen = loadingScreen;
            this.selfProfile = selfProfile;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, livekitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(loadingStatus, featureFlagsProvider, web3IdentityCache, decentralandUrlsSource, appParameters);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var switchRealmMiscVisibilityStartupOperation = new SwitchRealmMiscVisibilityStartupOperation(loadingStatus, realmNavigator);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, mainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, realmNavigator);
            checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, realmController, selfProfile, featureFlagsCache, decentralandUrlsSource);
            restartRealmStartupOperation = new RestartRealmStartupOperation(loadingStatus, realmController);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmNavigator, startParcel);

            startupOperation = new SequentialStartupOperation(
                loadingStatus,
                ensureLivekitConnectionStartupOperation,
                initializeFeatureFlagsStartupOperation,
                preloadProfileStartupOperation,
                switchRealmMiscVisibilityStartupOperation,
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                checkOnboardingStartupOperation,
                restartRealmStartupOperation,
                teleportStartupOperation
            ).WithHandleExceptions();
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            loadingStatus.SetStage(RealFlowLoadingStatus.Stage.Init);

            Result result = default;

            loadPlayerAvatarStartupOperation.AssignWorld(world, playerEntity);
            restartRealmStartupOperation.EnableReload(reloadRealm);

            using var playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (showAuthentication)
                {
                    loadingStatus.SetStage(RealFlowLoadingStatus.Stage.AuthenticationScreenShown);
                    await ShowAuthenticationScreenAsync(ct);
                }

                var loadingResult = await LoadingScreen(showLoading)
                   .ShowWhileExecuteTaskAsync(
                        async parentLoadReport => result = await startupOperation.ExecuteAsync(parentLoadReport, ct),
                        ct
                    );

                ApplyErrorIfLoadingScreenError(ref result, loadingResult);

                if (result.Success == false)
                    ReportHub.LogError(ReportCategory.DEBUG, result.ErrorMessage!);

                //TODO notification popup on failure
            }
            while (result.Success == false && showAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(world, playerEntity, ct);
        }

        private static void ApplyErrorIfLoadingScreenError(ref Result result, ILoadingScreen.LoadResult showResult)
        {
            if (!showResult.Success)
                result = Result.ErrorResult(showResult.ErrorMessage);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
