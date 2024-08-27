using DCL.AuthenticationScreenFlow;
using Segment.Serialization;
using System;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AuthenticationScreenAnalytics : IDisposable
    {
        private const string STAGE_KEY = "state";

        private readonly IAnalyticsController analytics;
        private readonly AuthenticationScreenController authenticationController;

        public AuthenticationScreenAnalytics(IAnalyticsController analytics, AuthenticationScreenController authenticationController)
        {
            this.analytics = analytics;
            this.authenticationController = authenticationController;

            authenticationController.CurrentState.OnUpdate += OnAuthenticationScreenStateChanged;
        }

        public void Dispose()
        {
            authenticationController.CurrentState.OnUpdate -= OnAuthenticationScreenStateChanged;
        }

        private void OnAuthenticationScreenStateChanged(AuthenticationScreenController.AuthenticationStatus state)
        {
            analytics.Track(AnalyticsEvents.General.INITIAL_LOADING, new JsonObject
            {
                { STAGE_KEY, $"7.0.{(byte)state} - authentication state: {state.ToString()}" },
            });
        }
    }
}
