using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class ProfileFetchingAuthState : AuthStateBase, IPayloadedState<(IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly SplashScreen splashScreen;
        private readonly ISelfProfile selfProfile;

        public ProfileFetchingAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            ReactiveProperty<AuthenticationStatus> currentState,
            SentryTransactionManager sentryTransactionManager,
            SplashScreen splashScreen,
            ISelfProfile selfProfile) : base(viewInstance)
        {
            this.machine = machine;
            this.currentState = currentState;
            this.sentryTransactionManager = sentryTransactionManager;
            this.splashScreen = splashScreen;
            this.selfProfile = selfProfile;
        }

        public void Enter((IWeb3Identity identity, bool isCached, CancellationToken ct) payload)
        {
            FetchProfileFlowAsync(payload.identity, payload.isCached, payload.ct).Forget();
        }

        public override void Exit()
        {
            if (machine.PreviousState is InitAuthState)
                splashScreen.FadeOutAndHide();
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

                    Profile? profile = await FetchProfileAsync(ct);
                    sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                    machine.Enter<LobbyForExistingAccountAuthState, (Profile, bool)>((profile, isCached));
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Profile not found during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Unexpected error during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.CONNECTION_ERROR, SLIDE));
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.RESTRICTED_USER, SLIDE));
            }
        }

        private async UniTask<Profile> FetchProfileAsync(CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            if (profile == null)
                throw new ProfileNotFoundException();

            // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
            profile.IsDirty = true;

            // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
            profile.HasConnectedWeb3 = true;
            return profile;
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
