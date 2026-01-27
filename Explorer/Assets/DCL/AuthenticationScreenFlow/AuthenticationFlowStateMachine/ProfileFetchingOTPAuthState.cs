using Cysharp.Threading.Tasks;
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
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow.AuthenticationFlowStateMachine
{
    public class ProfileFetchingOTPAuthState : AuthStateBase, IPayloadedState<(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly SentryTransactionManager sentryTransactionManager;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileFetchingAuthView view;

        public ProfileFetchingOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            SentryTransactionManager sentryTransactionManager,
            ISelfProfile selfProfile) : base(viewInstance)
        {
            view = viewInstance.ProfileFetchingAuthView;
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.sentryTransactionManager = sentryTransactionManager;
            this.selfProfile = selfProfile;
        }

        public void Enter((string email, IWeb3Identity identity, bool isCached, CancellationToken ct) payload)
        {
            view.Show();
            view.CancelButton.onClick.AddListener(controller.CancelLoginProcess);

            FetchProfileFlowAsync(payload.email, payload.identity, payload.isCached, payload.ct).Forget();
        }

        public override void Exit()
        {
            view.CancelButton.onClick.RemoveAllListeners();
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
                currentState.Value = isCached ? AuthStatus.FetchingProfileCached : AuthStatus.FetchingProfile;

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
                    view.Hide(OUT);

                    if (isNewUser)
                        machine.Enter<LobbyForNewAccountAuthState, (Profile, bool, CancellationToken)>((profile, false, ct));
                    else
                        machine.Enter<LobbyForExistingAccountAuthState, (Profile, bool)>((profile, isCached));
                }
                catch (OperationCanceledException)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
                }
                catch (ProfileNotFoundException e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Profile not found during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.NONE, SLIDE));
                }
                catch (Exception e)
                {
                    sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Unexpected error during {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.CONNECTION_ERROR, SLIDE));
                }
            }
            else
            {
                sentryTransactionManager.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, (PopupType type, int animHash)>((PopupType.RESTRICTED_USER, SLIDE));
            }
        }

        private async UniTask<(Profile profile, bool isNewUser)> FetchProfileAsync(string email, IWeb3Identity identity, CancellationToken ct)
        {
            Profile? profile = await selfProfile.ProfileAsync(ct);

            bool isNewUser = profile == null && ThirdWebManager.Instance.ActiveWallet != null;

            if (isNewUser)
                profile = Profile.NewRandomProfile(identity.Address.ToString());

            profile.HasClaimedName = false;
            profile.Description = string.Empty;
            profile.Country = string.Empty;
            profile.EmploymentStatus = string.Empty;
            profile.Gender = string.Empty;
            profile.Pronouns = string.Empty;
            profile.RelationshipStatus = string.Empty;
            profile.SexualOrientation = string.Empty;
            profile.Language = string.Empty;
            profile.Profession = string.Empty;
            profile.RealName = string.Empty;
            profile.Hobbies = string.Empty;
            profile.TutorialStep = 0;
            profile.Version = 0;
            profile.UserNameColor = NameColorHelper.GetNameColor(profile.DisplayName);

            profile.HasConnectedWeb3 = true;
            profile.IsDirty = true;

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
