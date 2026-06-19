using Cysharp.Threading.Tasks;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public class TrackedTokenFileAuthenticator : IWeb3Authenticator
    {
        private const int MAX_ERROR_MESSAGE_LENGTH = 300;

        private readonly TokenFileAuthenticator inner;
        private readonly IAnalyticsController analytics;

        public TrackedTokenFileAuthenticator(TokenFileAuthenticator inner, IAnalyticsController analytics)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            bool shouldTrack = inner.HasTokenFile();

            if (shouldTrack)
                analytics.Track(AnalyticsEvents.Authentication.AUTOLOGIN_INITIATED, isInstant: true);

            try
            {
                IWeb3Identity identity = await inner.LoginAsync(payload, ct);

                if (shouldTrack)
                {
                    analytics.Track(
                        AnalyticsEvents.Authentication.AUTOLOGIN_SUCCESS,
                        isInstant: true);
                }

                return identity;
            }
            catch (Exception e)
            {
                if (shouldTrack)
                {
                    string message = e.Message ?? string.Empty;
                    if (message.Length > MAX_ERROR_MESSAGE_LENGTH)
                        message = message[..MAX_ERROR_MESSAGE_LENGTH];

                    analytics.Track(
                        AnalyticsEvents.Authentication.AUTOLOGIN_FAILURE,
                        new JObject
                        {
                            ["error_type"] = e.GetType().Name,
                            ["error_message"] = message,
                        },
                        isInstant: true);
                }

                throw;
            }
        }

        public UniTask LogoutAsync(CancellationToken ct) =>
            inner.LogoutAsync(ct);

        public void Dispose() =>
            inner.Dispose();
    }
}

