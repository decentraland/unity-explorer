using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.PerformanceAndDiagnostics;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using DCL.Web3;
using DCL.Web3.Identities;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using ThirdWebUnity;
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class ProfileFetchingOTPAuthState : AuthStateBase, IPayloadedState<(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly ReactiveProperty<AuthenticationStatus> currentState;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly ISelfProfile selfProfile;

        public ProfileFetchingOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            ReactiveProperty<AuthenticationStatus> currentState,
            SentryTransactionManager sentryTransactionManager,
            ISelfProfile selfProfile) : base(viewInstance)
        {
            this.machine = machine;
            this.currentState = currentState;
            this.sentryTransactionManager = sentryTransactionManager;
            this.selfProfile = selfProfile;
        }

        public void Enter((string email, IWeb3Identity identity, bool isCached, CancellationToken ct) payload)
        {
            FetchProfileFlowAsync(payload.email, payload.identity, payload.isCached, payload.ct).Forget();
        }

        private async UniTaskVoid FetchProfileFlowAsync(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)
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

                    (Profile profile, bool isNewUser) = await FetchProfileAsync(email, identity, ct);
                    sentryTransactionManager.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                    if (isNewUser)
                        machine.Enter<LobbyOTPAuthState, (Profile, bool, CancellationToken)>((profile, false, ct));
                    else
                        machine.Enter<LobbyAuthState, (Profile, bool)>((profile, isCached));
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
                    machine.Enter<LoginStartAuthState, PopupType>(PopupType.CONNECTION_ERROR);
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                machine.Enter<LoginStartAuthState, PopupType>(PopupType.RESTRICTED_USER);
            }
        }

        private async UniTask<(Profile profile, bool isNewUser)> FetchProfileAsync(string email, IWeb3Identity identity, CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            bool isNewUser = profile == null && ThirdWebManager.Instance.ActiveWallet != null;

            if (isNewUser)
                profile = Profile.NewRandomProfile(identity.Address.ToString());

            profile!.IsDirty = true;
            profile.HasConnectedWeb3 = true;

            return (profile, isNewUser);
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
