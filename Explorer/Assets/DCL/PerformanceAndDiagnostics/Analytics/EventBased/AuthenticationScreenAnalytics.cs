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
        }

        public void Dispose()
        {
            controller.CurrentState.OnUpdate -= OnAuthenticationScreenStateChanged;
            controller.DiscordButtonClicked -= OnDiscordButtonClicked;
            controller.OTPVerified -= OnOTPVerified;
        }

        private void OnAuthenticationScreenStateChanged(AuthStatus state)
        {
            switch (state)
            {
                // Triggers when the user is already logged in
                case AuthStatus.LoggedInCached:
                    analytics.Track(Authentication.LOGGED_IN_CACHED, isInstant: true); break;

                // Triggers when the user is not logged in (login is requested)
                // TODO: We should also track the auth Request UUID here to link the explorer_v2 event with the auth page view and login events.
                case AuthStatus.LoginRequested:
                    analytics.Track(Authentication.LOGIN_REQUESTED, new JObject
                    {
                        { "provider", controller.CurrentProvider.ToString() },
                        { "method", controller.CurrentLoginMethod.ToString() },
                    });

                    break;

                // Triggered when the user tries to log in and is redirected to the authentication site
                case AuthStatus.VerificationInProgress:
                    analytics.Track(Authentication.VERIFICATION_REQUESTED, new JObject
                    {
                        { "requestID", controller.CurrentRequestID },
                    });

                    break;

                // Triggered when the user is logged in
                case AuthStatus.LoggedIn:
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
