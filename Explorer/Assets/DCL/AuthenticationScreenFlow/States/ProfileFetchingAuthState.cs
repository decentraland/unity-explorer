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
        private const int PROFILE_FETCH_ATTEMPTS = 3;
        private static readonly TimeSpan PROFILE_FETCH_TIMEOUT = TimeSpan.FromSeconds(15);

        private readonly MVCStateMachine<AuthStateBase> machine;
        private readonly AuthenticationScreenController controller;
        private readonly ReactiveProperty<AuthStatus> currentState;
        private readonly ISelfProfile selfProfile;
        private readonly IWeb3IdentityCache identityCache;
        private readonly ProfileFetchingAuthView view;
        private Exception? profileFetchException;

        public ProfileFetchingAuthState(
            MVCStateMachine<AuthStateBase> machine,
            AuthenticationScreenView viewInstance,
            AuthenticationScreenController controller,
            ReactiveProperty<AuthStatus> currentState,
            ISelfProfile selfProfile,
            IWeb3IdentityCache identityCache) : base(viewInstance)
        {
            view = viewInstance.ProfileFetchingAuthView;
            this.machine = machine;
            this.controller = controller;
            this.currentState = currentState;
            this.selfProfile = selfProfile;
            this.identityCache = identityCache;
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
                                    TimeoutException ex => new SpanErrorInfo($"Profile fetch timed out during {nameof(ProfileFetchingAuthState)}", ex),
                                    { } ex => new SpanErrorInfo($"Unexpected error during {nameof(ProfileFetchingAuthState)}", ex),
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
                machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.RestrictedUser);
            }
            else
            {
                SentryTransactionNameMapping.Instance.EndCurrentSpan(LOADING_TRANSACTION_NAME);
                currentState.Value = isCached ? AuthStatus.ProfileFetchingCached : AuthStatus.ProfileFetching;

                try
                {
                    SentryTransactionNameMapping.Instance.StartSpan(LOADING_TRANSACTION_NAME, new SpanData
                    {
                        SpanName = isCached ? "ProfileFetchingCached" : "ProfileFetching",
                        SpanOperation = "auth.profile_fetching",
                        Depth =  STATE_SPAN_DEPTH + 1,
                    });

                    // Timeout surfaces catalyst stalls as CONNECTION_ERROR instead of a frozen spinner.
                    if (await FetchProfileWithTimeoutRetriesAsync(selfProfile, PROFILE_FETCH_TIMEOUT, PROFILE_FETCH_ATTEMPTS, ct) is { } profile)
                    {
                        // When the profile was already in cache, for example your previous account after logout, we need to ensure that all systems related to the profile will update
                        profile.IsDirty = true;
                        // Catalysts don't manipulate this field, so at this point we assume that the user is connected to web3
                        profile.HasConnectedWeb3 = true;
                        machine.Enter<LobbyForExistingAccountAuthState, (Profile, bool, CancellationToken)>((profile, isCached, ct));
                    }
                    else if (isCached)
                    {
                        // Auto-login restored an identity that has no deployed profile (abandoned onboarding). Clear it and return to login selection.
                        identityCache.Clear();
                        profileFetchException = new ProfileNotFoundException();
                        machine.Enter<LoginSelectionAuthState, int>(SLIDE);
                    }
                    else
                    {
                        profile = CreateRandomProfile(identity.Address.ToString());
                        machine.Enter<LobbyForNewAccountAuthState, (Profile, string, bool, CancellationToken)>((profile, email, false, ct)); // email is only used for optional newsletter subscription
                    }
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
                catch (TimeoutException e)
                {
                    profileFetchException = e;
                    machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.ConnectionError);
                }
                catch (Exception e)
                {
                    profileFetchException = e;
                    machine.Enter<LoginSelectionAuthState, ErrorType>(ErrorType.ConnectionError);
                }
            }
        }

        /// <summary>
        ///     Each attempt owns a linked token, so a timed-out attempt cancels its underlying request instead of
        ///     abandoning it. Only exhausting all attempts surfaces as <see cref="TimeoutException" /> (CONNECTION_ERROR);
        ///     cancellation of <paramref name="ct" /> surfaces as <see cref="OperationCanceledException" />.
        /// </summary>
        internal static async UniTask<Profile?> FetchProfileWithTimeoutRetriesAsync(ISelfProfile selfProfile, TimeSpan attemptTimeout, int maxAttempts, CancellationToken ct)
        {
            for (var attempt = 1;; attempt++)
            {
                using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                using IDisposable timeoutTimer = timeoutCts.CancelAfterSlim(attemptTimeout);

                if (await selfProfile.ProfileAsync(timeoutCts.Token) is { } profile)
                    return profile;

                // The repository suppresses cancellation into a null profile, including cancellation of the flow token.
                // Surface external cancellation as OCE so it is classified as a user cancel, not as "no deployed profile"
                // (which on the cached flow would clear a still-valid stored identity)
                ct.ThrowIfCancellationRequested();

                if (!timeoutCts.IsCancellationRequested)
                    return null; // genuine "no deployed profile"

                if (attempt >= maxAttempts)
                    throw new TimeoutException($"Profile fetch timed out after {maxAttempts} attempts of {attemptTimeout.TotalSeconds:F0}s each");
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
