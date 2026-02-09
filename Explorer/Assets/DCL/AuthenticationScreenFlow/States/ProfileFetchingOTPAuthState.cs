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
using UnityEngine;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.UI.UIAnimationHashes;

namespace DCL.AuthenticationScreenFlow
{
    public class ProfileFetchingOTPAuthState : AuthStateBase, IPayloadedState<(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileFetchingAuthView view;

        public ProfileFetchingOTPAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ISelfProfile selfProfile) : base(viewInstance)
        {
            view = viewInstance.ProfileFetchingAuthView;
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
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
                SpanName = "IdentityValidation",
                SpanOperation = "auth.identity_validation",
                Depth = 1,
            };

            SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, identityValidationSpan);

            if (!IsUserAllowedToAccessToBeta(identity))
            {
                SentryTransactionNameMapping.Instance.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"User not allowed to access beta - restricted user in {nameof(ProfileFetchingOTPAuthState)} ({(isCached ? "cached" : "main")} flow)");
                view.Hide(SLIDE);
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.RESTRICTED_USER);
            }
            else
            {
                currentState.Value = isCached ? AuthStatus.FetchingProfileCached : AuthStatus.FetchingProfile;

                // Close IdentityValidation span before starting profile fetch
                SentryTransactionNameMapping.Instance.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                try
                {
                    var profileFetchSpan = new SpanData
                    {
                        SpanName = "FetchProfileCached",
                        SpanOperation = "auth.profile_fetch",
                        Depth = 1,
                    };

                    SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, profileFetchSpan);

                    Profile? profile = await selfProfile.ProfileAsync(ct);

                    SentryTransactionNameMapping.Instance.EndCurrentSpan(LOADING_TRANSACTION_NAME);

                    view.Hide(OUT);

                    if (profile == null)
                    {
                        profile = CreateRandomProfile(identity.Address.ToString());
                        machine.Enter<LobbyForNewAccountAuthState, (Profile, string, bool, CancellationToken)>((profile, email, false, ct));
                    }
                    else
                    {
                        // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
                        profile.IsDirty = true;
                        // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
                        profile.HasConnectedWeb3 = true;
                        machine.Enter<LobbyForExistingAccountAuthState, (Profile, bool, CancellationToken)>((profile, isCached, ct));
                    }
                }
                catch (OperationCanceledException)
                {
                    SentryTransactionNameMapping.Instance.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, "Login process was cancelled by user");
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, int>(SLIDE);
                }
                catch (ProfileNotFoundException e)
                {
                    SentryTransactionNameMapping.Instance.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Profile not found during {nameof(ProfileFetchingOTPAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, int>(SLIDE);
                }
                catch (Exception e)
                {
                    SentryTransactionNameMapping.Instance.EndCurrentSpanWithError(LOADING_TRANSACTION_NAME, $"Unexpected error during {nameof(ProfileFetchingOTPAuthState)} ({(isCached ? "cached" : "main")} flow)", e);
                    ReportHub.LogException(e, new ReportData(ReportCategory.AUTHENTICATION));
                    view.Hide(SLIDE);
                    machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.CONNECTION_ERROR);
                }
            }
        }

        private Profile CreateRandomProfile(string identityAddress)
        {
            var profile = Profile.NewRandomProfile(identityAddress);
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

            profile.HasConnectedWeb3 = true;
            profile.IsDirty = true;

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
