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
        }

        public void Dispose()
        {
            controller.CurrentState.OnUpdate -= OnAuthenticationScreenStateChanged;
            controller.DiscordButtonClicked -= OnDiscordButtonClicked;
            controller.OTPVerified -= OnOTPVerified;
            controller.OTPResend -= OnOTPResend;
        }

        private void OnAuthenticationScreenStateChanged(AuthStatus state)
        {
            switch (state)
            {
                case AuthStatus.LoginSelectionScreen:
                    analytics.Track(Authentication.LOGIN_SELECTION_SCREEN);
                    break;

                // Triggers when the user is not logged in (login is requested)
                // TODO: We should also track the auth Request UUID here to link the explorer_v2 event with the auth page view and login events.
                case AuthStatus.LoginRequested:
                    analytics.Track(Authentication.LOGIN_REQUESTED, new JObject
                    {
                        { "method", controller.CurrentLoginMethod.ToString() },
                    });

                    break;

                // Triggered when the user tries to log in and is redirected to the authentication site
                case AuthStatus.VerificationRequested:
                    analytics.Track(Authentication.VERIFICATION_REQUESTED, new JObject
                    {
                        { "requestID", controller.CurrentRequestID },
                    });

                    break;

                // Triggered when the user is logged in
                case AuthStatus.LoggedIn:
                    analytics.Track(Authentication.LOGGED_IN, new JObject
                    {
                        { "method", controller.CurrentLoginMethod.ToString() },
                    }, isInstant: true);

                    break;

                // Triggers when the user is already logged in (has valid Identity)
                case AuthStatus.LoggedInCached:
                    analytics.Track(Authentication.LOGGED_IN_CACHED, isInstant: true); break;
                case AuthStatus.FetchingProfile: break;
                case AuthStatus.FetchingProfileCached: break;
                default: throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

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
