using DCL.AuthenticationScreenFlow;
using Newtonsoft.Json.Linq;
using System;
using static DCL.AuthenticationScreenFlow.AuthenticationScreenController;
using static DCL.PerformanceAndDiagnostics.Analytics.AnalyticsEvents;

namespace DCL.PerformanceAndDiagnostics.Analytics.EventBased
{
    public class AuthenticationScreenAnalytics : IDisposable
    {
        private readonly IAnalyticsController analytics;
        private readonly AuthenticationScreenController controller;

        public AuthenticationScreenAnalytics(IAnalyticsController analytics, AuthenticationScreenController controller)
        {
            this.analytics = analytics;
            this.controller = controller;
            controller.CurrentState.OnUpdate += OnAuthenticationScreenStateChanged;
            controller.DiscordButtonClicked += OnDiscordButtonClicked;
            controller.OTPVerified += OnOTPVerified;
            controller.OTPResend += OnOTPResend;
            controller.ProfileFinalized += OnProfileFinalized;
        }

        public void Dispose()
        {
            controller.CurrentState.OnUpdate -= OnAuthenticationScreenStateChanged;
            controller.DiscordButtonClicked -= OnDiscordButtonClicked;
            controller.OTPVerified -= OnOTPVerified;
            controller.OTPResend -= OnOTPResend;
            controller.ProfileFinalized -= OnProfileFinalized;
        }

        private void OnAuthenticationScreenStateChanged(AuthStatus state)
        {
            UnityEngine.Debug.LogError($"[DCL Analytics DEBUG] OnAuthenticationScreenStateChanged fired with state={state}, IsCurrentlyNewAccount={controller.IsCurrentlyNewAccount}, CurrentLoginMethod={controller.CurrentLoginMethod}");
            switch (state)
            {
                // Triggered WHEN login screen is shown
                case AuthStatus.LoginSelectionScreen:
                    analytics.Track(Authentication.LOGIN_SELECTION_SCREEN);
                    break;

                // Triggered WHEN the user press one of the login buttons in the Login Selection Screen
                case AuthStatus.LoginRequested:
                    analytics.Track(Authentication.LOGIN_REQUESTED, new JObject
                    {
                        { "method", controller.CurrentLoginMethod.ToString() },
                    });
                    break;

                // Triggered WHEN verification screen is shown (dapp code or OTP)
                case AuthStatus.VerificationRequested:
                    analytics.Track(Authentication.VERIFICATION_REQUESTED, new JObject
                    {
                        { "requestID", controller.CurrentRequestID },
                    });
                    break;

                case AuthStatus.ProfileFetching:
                    analytics.Track(Authentication.PROFILE_FETCHING);
                    // Auth flow finished and the engine is now resolving the profile. Pair this
                    // with LOGIN_REQUESTED to measure the auth-attempt → auth-success conversion;
                    // a LOGIN_REQUESTED with no AUTH_COMPLETED within a session = failed auth.
                    analytics.Track(Authentication.AUTH_COMPLETED, new JObject
                    {
                        { "method", controller.CurrentLoginMethod.ToString() },
                        { "is_cached", false },
                    });
                    break;
                case AuthStatus.LoggedIn: // Triggered WHEN the user gets in Lobby
                    UnityEngine.Debug.LogError("[DCL Analytics DEBUG] LoggedIn case entered, about to Track LOGGED_IN");
                    try
                    {
                        analytics.Track(Authentication.LOGGED_IN, new JObject
                        {
                            { "method", controller.CurrentLoginMethod.ToString() },
                            { "is_new_account", controller.IsCurrentlyNewAccount },
                        }, isInstant: true);
                        UnityEngine.Debug.LogError("[DCL Analytics DEBUG] LOGGED_IN Track returned successfully");
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError($"[DCL Analytics DEBUG] LOGGED_IN Track THREW: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                        throw;
                    }
                    if (controller.IsCurrentlyNewAccount)
                    {
                        UnityEngine.Debug.LogError("[DCL Analytics DEBUG] IsCurrentlyNewAccount=true, about to Track NEW_ACCOUNT_ONBOARDING_STARTED");
                        try
                        {
                            // isInstant: true — this is the start of the in-Explorer onboarding step
                            // and pairs with PROFILE_FINALIZED. We need both endpoints of the
                            // onboarding measurement to be guaranteed-flushed; otherwise an early
                            // abandon (close client during avatar customization) can drop the start
                            // event and leave us with a profile_finalized that has no opener.
                            analytics.Track(Authentication.NEW_ACCOUNT_ONBOARDING_STARTED, new JObject
                            {
                                { "method", controller.CurrentLoginMethod.ToString() },
                            }, isInstant: true);
                            UnityEngine.Debug.LogError("[DCL Analytics DEBUG] NEW_ACCOUNT_ONBOARDING_STARTED Track returned successfully");
                        }
                        catch (System.Exception e)
                        {
                            UnityEngine.Debug.LogError($"[DCL Analytics DEBUG] NEW_ACCOUNT_ONBOARDING_STARTED Track THREW: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                            throw;
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError("[DCL Analytics DEBUG] IsCurrentlyNewAccount=FALSE, skipping NEW_ACCOUNT_ONBOARDING_STARTED");
                    }
                    break;

                // CACHED FLOW - when the user is already logged in (has valid Identity)
                case AuthStatus.ProfileFetchingCached:
                    analytics.Track(Authentication.PROFILE_FETCHING_CACHED);
                    analytics.Track(Authentication.AUTH_COMPLETED, new JObject
                    {
                        { "method", controller.CurrentLoginMethod.ToString() },
                        { "is_cached", true },
                    });
                    break;
                case AuthStatus.LoggedInCached: // Triggered WHEN the user gets in Lobby
                    analytics.Track(Authentication.LOGGED_IN_CACHED, new JObject
                    {
                        { "is_new_account", controller.IsCurrentlyNewAccount },
                    }, isInstant: true);
                    if (controller.IsCurrentlyNewAccount)
                    {
                        // isInstant: true — same reasoning as the LoggedIn branch.
                        analytics.Track(Authentication.NEW_ACCOUNT_ONBOARDING_STARTED, new JObject
                        {
                            { "method", controller.CurrentLoginMethod.ToString() },
                        }, isInstant: true);
                    }
                    break;

                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void OnProfileFinalized() =>
            // isInstant: true because this fires moments before the auth screen tears down
            // and the FSM transitions to InitAuthState. Without flushing immediately the event
            // can sit in the buffer past the screen disposal and never make it to Segment.
            analytics.Track(Authentication.PROFILE_FINALIZED, new JObject
            {
                { "method", controller.CurrentLoginMethod.ToString() },
            }, isInstant: true);

        private void OnOTPVerified(string email, bool success)
        {
            analytics.Track(success ? Authentication.OTP_VERIFICATION_SUCCESS : Authentication.OTP_VERIFICATION_FAILURE, new JObject
            {
                { "email", email },
            });
        }

        private void OnOTPResend() =>
            analytics.Track(Authentication.OTP_RESEND);

        private void OnDiscordButtonClicked() =>
            analytics.Track(Authentication.CLICK_COMMUNITY_GUIDANCE);
    }
}
