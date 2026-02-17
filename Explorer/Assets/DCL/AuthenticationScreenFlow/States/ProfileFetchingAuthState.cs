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
    public class ProfileFetchingAuthState : AuthStateBase, IPayloadedState<ProfileFetchingPayload>
    {
        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ISelfProfile selfProfile;
        private readonly ProfileFetchingAuthView view;
        private Exception? profileFetchException;

        public ProfileFetchingAuthState(
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

        public void Enter(ProfileFetchingPayload payload)
        {
            base.Enter();
            profileFetchException = null;

            view.Show();
            view.CancelButton.onClick.AddListener(controller.CancelLoginProcess);

            FetchProfileFlowAsync(payload.Email, payload.Identity, payload.IsCached, payload.Ct).Forget();
        }

        public override void Exit()
        {
            if (profileFetchException == null)
                view.Hide(OUT);
            else
            {
                view.Hide(SLIDE);

                spanErrorInfo = profileFetchException switch
                                {
                                    OperationCanceledException => new SpanErrorInfo("Login process was cancelled by user"),
                                    ProfileNotFoundException ex => new SpanErrorInfo($"Profile not found during {nameof(ProfileFetchingAuthState)}", ex),
                                    NotAllowedUserException ex => new SpanErrorInfo(ex.Message, ex),
                                    Exception ex => new SpanErrorInfo($"Unexpected error during {nameof(ProfileFetchingAuthState)}", ex),
                                };

                if (profileFetchException is not OperationCanceledException and not ProfileNotFoundException and not NotAllowedUserException)
                    ReportHub.LogException(profileFetchException, new ReportData(ReportCategory.AUTHENTICATION));
            }

            view.CancelButton.onClick.RemoveAllListeners();
            base.Exit();
        }

        private async UniTaskVoid FetchProfileFlowAsync(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)
        {
            SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, new SpanData
            {
                SpanName = "IdentityAuthorization",
                SpanOperation = "auth.identity_authorization",
                Depth = STATE_SPAN_DEPTH + 1,
            });

            if (!IsUserAllowedToAccessToBeta(identity))
            {
                profileFetchException = new NotAllowedUserException($"User not allowed to access beta - restricted user {email} in {nameof(ProfileFetchingAuthState)} ({(isCached ? "cached" : "main")} flow)");
                machine.Enter<LoginSelectionAuthState, PopupType>(PopupType.RESTRICTED_USER);
            }
            else
            {
                SentryTransactionNameMapping.Instance.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                currentState.Value = isCached ? AuthStatus.ProfileFetchingCached : AuthStatus.ProfileFetching;

                try
                {
                    SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, new SpanData
                    {
                        SpanName = isCached ? "FetchProfileCached" : "FetchProfile",
                        SpanOperation = "auth.profile_fetch",
                        Depth =  STATE_SPAN_DEPTH + 1,
                    });

                    Profile? profile = await selfProfile.ProfileAsync(ct);

                    if (profile != null)
                    {
                        // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
                        profile.IsDirty = true;
                        // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
                        profile.HasConnectedWeb3 = true;
                        machine.Enter<LobbyForExistingAccountAuthState, (Profile, bool, CancellationToken)>((profile, isCached, ct));
                    }
                    else if (!string.IsNullOrEmpty(email)) // in case of OTP flow we create new Profile and proceed
                    {
                        profile = CreateRandomProfile(identity.Address.ToString());
                        machine.Enter<LobbyForNewAccountAuthState, (Profile, string, bool, CancellationToken)>((profile, email, false, ct));
                    }
                    else
                        throw new ProfileNotFoundException();
                }
                catch (OperationCanceledException e)
                {
                    profileFetchException = e;
                    machine.Enter<LoginSelectionAuthState, int>(SLIDE);
                }
                catch (ProfileNotFoundException e)
                {
                    profileFetchException = e;
                    machine.Enter<LoginSelectionAuthState, int>(SLIDE);
                }
                catch (Exception e)
                {
                    profileFetchException = e;
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

    public struct ProfileFetchingPayload
    {
        public readonly string Email;
        public readonly IWeb3Identity Identity;
        public readonly bool IsCached;
        public CancellationToken Ct;

        public ProfileFetchingPayload(string email, IWeb3Identity identity, bool isCached, CancellationToken ct)
        {
            this.Email = email;
            this.Identity = identity;
            this.IsCached = isCached;
            this.Ct = ct;
        }

        public ProfileFetchingPayload(IWeb3Identity identity, bool isCached, CancellationToken ct)
        {
            this.Email = string.Empty;
            this.Identity = identity;
            this.IsCached = isCached;
            this.Ct = ct;
        }
    }
}
