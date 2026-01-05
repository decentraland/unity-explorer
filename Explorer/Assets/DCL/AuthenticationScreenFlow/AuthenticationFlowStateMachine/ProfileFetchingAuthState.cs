using Cysharp.Threading.Tasks;
using DCL.CharacterPreview;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.UI;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using Utility;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class ProfileFetchingAuthState : AuthStateBase, IPayloadedState<(IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly SplashScreen splashScreen;
        private readonly AuthenticationScreenCharacterPreviewController characterPreviewController;
        private readonly ISelfProfile selfProfile;

        private readonly StringVariable? profileNameLabel;

        public ProfileFetchingAuthState(AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthenticationStatus> currentState,
            SentryTransactionManager sentryTransactionManager,
            SplashScreen splashScreen,
            AuthenticationScreenCharacterPreviewController characterPreviewController,
            ISelfProfile selfProfile) : base(viewInstance)
        {
            this.controller = controller;
            this.currentState = currentState;
            this.sentryTransactionManager = sentryTransactionManager;
            this.splashScreen = splashScreen;
            this.characterPreviewController = characterPreviewController;
            this.selfProfile = selfProfile;

            profileNameLabel = (StringVariable)viewInstance!.ProfileNameLabel.StringReference["back_profileName"];
        }

        public override void Enter()
        {
            // TODO (Vit): remove after refactoring state-machine to interfaces instead of its current CRTP (Curiously Recurring Template Pattern) approach
            ReportHub.LogError(ReportCategory.AUTHENTICATION, $"Trying to enter state {nameof(ProfileFetchingAuthState)} that doesn't support entering without payload.");
        }

        public void Enter((IWeb3Identity identity, bool isCached, CancellationToken ct) payload)
        {
            base.Enter();
            FetchProfileFlowAsync(payload.identity, payload.isCached, payload.ct).Forget();
        }

        public override void Exit()
        {
            base.Exit();

            if (machine.PreviousState is InitAuthScreenState)
                splashScreen.Hide();
        }

        private async UniTaskVoid FetchProfileFlowAsync(IWeb3Identity identity, bool isCached, CancellationToken ct)
        {
            var identityValidationSpan = new SpanData
            {
                TransactionName = LOADING_TRANSACTION_NAME,
                SpanName = "IdentityValidation",
                SpanOperation = "auth.identity_validation",
                Depth = 1,
            };

            sentryTransactionManager.StartSpan(identityValidationSpan);

            if (IsUserAllowedToAccessToBeta(identity))
            {
                currentState.Value = isCached ? AuthenticationStatus.FetchingProfileCached : AuthenticationStatus.FetchingProfile;

                try
                {
                    var profileFetchSpan = new SpanData
                    {
                        TransactionName = LOADING_TRANSACTION_NAME,
                        SpanName = "FetchProfileCached",
                        SpanOperation = "auth.profile_fetch",
                        Depth = 1,
                    };

                    sentryTransactionManager.StartSpan(profileFetchSpan);

                    await FetchProfileAsync(ct);
                    sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                    currentState.Value = isCached ? AuthenticationStatus.LoggedInCached : AuthenticationStatus.LoggedIn;
                    machine.Enter<LobbyAuthState>();
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    machine.Enter<LoginStartAuthState>();
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Profile not found during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    machine.Enter<LoginStartAuthState>();
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Unexpected error during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    machine.Enter<LoginStartAuthState>();
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                machine.Enter<LoginStartAuthState, PopupType>(PopupType.RESTRICTED_USER);
            }
        }

        private void ShowLoadingSpinner()
        {
            viewInstance.LoginContainer.SetActive(true);

            viewInstance.LoginAnimator.ResetAnimator();
            viewInstance.LoginAnimator.SetTrigger(UIAnimationHashes.IN);

            viewInstance.LoginButton.gameObject.SetActive(false);
            viewInstance.LoginButton.interactable = false;

            viewInstance.LoadingSpinner.SetActive(true);
        }

        private async UniTask FetchProfileAsync(CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null)
                throw new ProfileNotFoundException();

            // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
            profile.IsDirty = true;

            // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
            profile.HasConnectedWeb3 = true;

            profileNameLabel!.Value = IsNewUser() ? profile.Name : "back " + profile.Name;
            characterPreviewController?.Initialize(profile.Avatar, CharacterPreviewUtils.AVATAR_POSITION_2);

            return;

            bool IsNewUser() =>
                profile.Version == 1;
        }

        private static bool IsUserAllowedToAccessToBeta(IWeb3Identity storedIdentity)
        {
            if (Application.isEditor)
                return true;

            FeatureFlagsConfiguration flags = FeatureFlagsConfiguration.Instance;

            if (!flags.IsEnabled(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT)) return true;

            if (!flags.TryGetCsvPayload(FeatureFlagsStrings.USER_ALLOW_LIST, FeatureFlagsStrings.WALLET_VARIANT, out List<List<string>>? allowedUsersCsv))
                return true;

            bool isUserAllowed = allowedUsersCsv![0]
               .Exists(s => new Web3Address(s).Equals(storedIdentity.Address));

            return isUserAllowed;
        }
    }
}
