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
        private readonly AuthenticationScreenController authenticationController;

        public AuthenticationScreenAnalytics(IAnalyticsController analytics, AuthenticationScreenController authenticationController)
        {
            this.analytics = analytics;
            this.authenticationController = authenticationController;
            authenticationController.CurrentState.OnUpdate += OnAuthenticationScreenStateChanged;
            authenticationController.DiscordButtonClicked += OnDiscordButtonClicked;
            authenticationController.OTPVerified += OnOTPVerified;
        }

        public void Dispose()
        {
            authenticationController.CurrentState.OnUpdate -= OnAuthenticationScreenStateChanged;
            authenticationController.DiscordButtonClicked -= OnDiscordButtonClicked;
            authenticationController.OTPVerified -= OnOTPVerified;
        }

        private void OnAuthenticationScreenStateChanged(AuthenticationStatus state)
        {
            switch (state)
            {
                // Triggers when the user is already logged in
                case AuthenticationStatus.LoggedInCached:
                    analytics.Track(Authentication.LOGGED_IN_CACHED, isInstant: true); break;

                // Triggers when the user is not logged in (login is requested)
                // TODO: We should also track the auth Request UUID here to link the explorer_v2 event with the auth page view and login events.
                case AuthenticationStatus.Login:
                    analytics.Track(Authentication.LOGIN_REQUESTED); break;

                // Triggered when the user tries to log in and is redirected to the authentication site
                case AuthenticationStatus.VerificationInProgress:
                    analytics.Track(Authentication.VERIFICATION_REQUESTED, new JObject
                    {
                        { "requestID", authenticationController.CurrentRequestID },
                    }); break;

                // Triggered when the user is logged in
                case AuthenticationStatus.LoggedIn:
                    analytics.Track(Authentication.LOGGED_IN, isInstant: true); break;
            }
        }

        private void OnDiscordButtonClicked() =>
            analytics.Track(Authentication.CLICK_COMMUNITY_GUIDANCE);

        private void OnOTPVerified(bool success) =>
            analytics.Track(Authentication.OTP_VERIFIED, new JObject
            {
                { "success", success },
            });
    }
}
