using System;
using System.Threading;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UI.ErrorPopup;
using DCL.UserInAppInitializationFlow.StartupOperations;
using ECS.SceneLifeCycle.Realm;
using MVC;
using PortableExperiences.Controller;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInAppInitializationFlow : IUserInAppInitializationFlow
    {
        private static readonly ILoadingScreen.EmptyLoadingScreen EMPTY_LOADING_SCREEN = new ();

        private readonly ILoadingStatus loadingStatus;
        private readonly IMVCManager mvcManager;
        private readonly AudioClipConfig backgroundMusic;
        private readonly IRealmNavigator realmNavigator;
        private readonly ILoadingScreen loadingScreen;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IChatHistory chatHistory;
        private readonly SequentialLoadingOperation<IStartupOperation.Params> initOps;
        private readonly SequentialLoadingOperation<IStartupOperation.Params> reloginOps;

        private readonly IRealmController realmController;
        private readonly IRoomHub roomHub;
        private readonly IPortableExperiencesController portableExperiencesController;
        private readonly CheckOnboardingStartupOperation checkOnboardingStartupOperation;

        public RealUserInAppInitializationFlow(
            ILoadingStatus loadingStatus,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IRealmController realmController,
            IPortableExperiencesController portableExperiencesController,
            IRoomHub roomHub,
            IChatHistory chatHistory,
            SequentialLoadingOperation<IStartupOperation.Params> initOps,
            SequentialLoadingOperation<IStartupOperation.Params> reloginOps,
            CheckOnboardingStartupOperation checkOnboardingStartupOperation)
        {
            this.initOps = initOps;
            this.reloginOps = reloginOps;
            this.checkOnboardingStartupOperation = checkOnboardingStartupOperation;

            this.loadingStatus = loadingStatus;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.realmController = realmController;
            this.portableExperiencesController = portableExperiencesController;
            this.chatHistory = chatHistory;
            this.roomHub = roomHub;
        }


        public async UniTask ExecuteAsync(UserInAppInitializationFlowParameters parameters, CancellationToken ct)
        {
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Init);

            EnumResult<TaskError> result = parameters.RecoveryError;

            using UIAudioEventsBus.PlayAudioScope playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (parameters.ShowAuthentication)
                {
                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.AuthenticationScreenShowing);

                    switch (parameters.LoadSource)
                    {
                        case IUserInAppInitializationFlow.LoadSource.Logout:
                            await DoLogoutOperationsAsync();

                            //Restart the realm and show the authentications screen simultaneously to avoid the "empty space" flicker
                            //No error should be possible at this point
                            // TODO move SetRealmAsync to an operation
                            await UniTask.WhenAll(ShowAuthenticationScreenAsync(ct),
                                realmController.SetRealmAsync(
                                    URLDomain.FromString(decentralandUrlsSource.Url(DecentralandUrl.Genesis)), ct));

                            break;
                        case IUserInAppInitializationFlow.LoadSource.Recover:
                            await DoRecoveryOperationsAsync();
                            goto default;
                        default:
                            await UniTask.WhenAll(
                                ShowAuthenticationScreenAsync(ct),
                                ShowErrorPopupIfRequired(result, ct)
                            );

                            break;
                    }
                }

                var flowToRun = parameters.LoadSource is IUserInAppInitializationFlow.LoadSource.Logout
                    ? reloginOps
                    : initOps;

                var loadingResult = await LoadingScreen(parameters.ShowLoading)
                    .ShowWhileExecuteTaskAsync(
                        async (parentLoadReport, ct) =>
                        {
                            EnumResult<TaskError> operationResult = await flowToRun.ExecuteAsync(parameters.LoadSource.ToString(), 1, new IStartupOperation.Params(parentLoadReport, parameters), ct);
                            if (operationResult.Success)
                                parentLoadReport.SetProgress(
                                    loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed));
                            return operationResult;
                        },
                        ct
                    );

                result = loadingResult;

                if (result.Success == false)
                {
                    string message = result.Error.AsMessage();
                    ReportHub.LogError(ReportCategory.AUTHENTICATION, message);
                }
            }
            while (result.Success == false && parameters.ShowAuthentication);

            await checkOnboardingStartupOperation.MarkOnboardingAsDoneAsync(parameters.World, parameters.PlayerEntity, ct);
            loadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.Completed);
        }

        // TODO should be an operation
        private async UniTask DoLogoutOperationsAsync()
        {
            portableExperiencesController.UnloadAllPortableExperiences();
            realmNavigator.RemoveCameraSamplingData();
            chatHistory.DeleteAllChannels();
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
        }

        // TODO should be an operation
        private async UniTask DoRecoveryOperationsAsync()
        {
            await roomHub.StopAsync().Timeout(TimeSpan.FromSeconds(10));
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private UniTask ShowErrorPopupIfRequired(EnumResult<TaskError> result, CancellationToken ct)
        {
            if (result.Success)
                return UniTask.CompletedTask;

            var message = $"{ToMessage(result)}\nPlease try again";
            return mvcManager.ShowAsync(new ShowCommand<ErrorPopupView, ErrorPopupData>(ErrorPopupData.FromDescription(message)), ct);
        }

        private string ToMessage(EnumResult<TaskError> result)
        {
            if (result.Success)
            {
                ReportHub.LogError(ReportCategory.AUTHENTICATION, "Incorrect use case of error to message");
                return "Incorrect error state";
            }

            var error = result.Error!.Value;

            return error.State switch
                   {
                       TaskError.MessageError => $"Error: {error.Message}",
                       TaskError.Timeout => "Load timeout",
                       TaskError.Cancelled => "Operation cancelled",
                       TaskError.UnexpectedException => "Critical error occured",
                       _ => throw new ArgumentOutOfRangeException()
                   };
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
