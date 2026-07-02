using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.RuntimeDeepLink;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Production wallet sign-in via browser and OS deep link: initiates a sign-in request on the auth server,
    ///     opens the browser for the user to sign with their wallet, then awaits the deep link
    ///     (via <see cref="IDeeplinkSigninDispatcher" />) and resolves the resulting identity from the server.
    /// </summary>
    public class DappDeepLinkAuthenticator : IWeb3Authenticator
    {
        // Fallback session length when no explicit override is provided (days).
        private const double IDENTITY_EXPIRATION_PERIOD = 30;

        // The deep-link flow waits for the user to sign in their browser and for the OS to route the resulting
        // deep link back to this process; this can take much longer than a socket round-trip.
        private const int DEEPLINK_TIMEOUT_SECONDS = 300;

        private readonly IWebBrowser webBrowser;
        private readonly URLAddress authApiUrl;
        private readonly URLAddress signatureWebAppUrl;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly IWebRequestController webRequestController;
        private readonly IDeeplinkSigninDispatcher deeplinkSigninDispatcher;
        private readonly int? identityExpirationDuration;
        private readonly URLBuilder urlBuilder = new ();

        public DappDeepLinkAuthenticator(
            IWebBrowser webBrowser,
            URLAddress authApiUrl,
            URLAddress signatureWebAppUrl,
            IWeb3AccountFactory web3AccountFactory,
            IWebRequestController webRequestController,
            IDeeplinkSigninDispatcher deeplinkSigninDispatcher,
            int? identityExpirationDuration = null)
        {
            this.webBrowser = webBrowser;
            this.authApiUrl = authApiUrl;
            this.signatureWebAppUrl = signatureWebAppUrl;
            this.web3AccountFactory = web3AccountFactory;
            this.webRequestController = webRequestController;
            this.deeplinkSigninDispatcher = deeplinkSigninDispatcher;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public void Dispose() { }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            // The local ephemeral is not used to build the final identity (the browser owns the real keypair; we
            // resolve via the fetcher), but it is the well-formed message the server signs into the minted request.
            var ephemeralAccount = web3AccountFactory.CreateRandomAccount();

            DateTime sessionExpiration = identityExpirationDuration != null
                ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                : DateTime.UtcNow.AddDays(IDENTITY_EXPIRATION_PERIOD);

            string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

            CreateRequestResponseDto createRequestResponse = await CreateSigninRequestAsync(ephemeralMessage, ct);

            if (string.IsNullOrEmpty(createRequestResponse.requestId))
                throw new Web3Exception("Cannot solve auth request id");

            // OpenUrl routes through Application.OpenURL, which must run on the main thread.
            await UniTask.SwitchToMainThread(ct);

            string url = $"{signatureWebAppUrl}/{createRequestResponse.requestId}?loginMethod={payload.Method}&flow=deeplink";

            webBrowser.OpenUrl(url);

            // The browser builds and stores the AuthIdentity, then opens decentraland://?signin={identityId};
            // the OS routes it to DeepLinkHandle, which dispatches it here.
            string identityId = await WaitForSigninAsync(ct);

            return await FetchIdentityByIdAsync(identityId, ct);
        }

        public UniTask LogoutAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        /// <summary>
        ///     Mints a sign-in <c>requestId</c> via <c>POST {authApiUrl}/requests</c>. The browser later recovers the
        ///     request by that id to drive the wallet signature.
        /// </summary>
        private async UniTask<CreateRequestResponseDto> CreateSigninRequestAsync(string ephemeralMessage, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath("requests"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            string body = JsonConvert.SerializeObject(new SigninRequestDto
            {
                method = "dcl_personal_sign",
                @params = new object[] { ephemeralMessage },
            });

            return await webRequestController.PostAsync(commonArguments, GenericPostArguments.CreateJson(body), ct, ReportCategory.AUTHENTICATION)
                                             .CreateFromNewtonsoftJsonAsync<CreateRequestResponseDto>();
        }

        /// <summary>
        ///     Awaits the first <c>identityId</c> the dispatcher delivers, applying a timeout and honoring external
        ///     cancellation. Disposing the subscription on completion clears the dispatcher buffer so a deep link consumed
        ///     (or cancelled) by one attempt does not bleed into the next.
        /// </summary>
        private async UniTask<string> WaitForSigninAsync(CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<string>();

            using IDisposable subscription = deeplinkSigninDispatcher.Subscribe(identityId => completionSource.TrySetResult(identityId));

            // Realtime, not the default DeltaTime: the wait spans the user switching to the browser and back, during
            // which the app is backgrounded and game time stalls. Only wall-clock measures the deadline correctly.
            return await completionSource.Task.Timeout(TimeSpan.FromSeconds(DEEPLINK_TIMEOUT_SECONDS), DelayType.Realtime).AttachExternalCancellation(ct);
        }

        /// <summary>
        ///     Fetches a stored identity by its opaque id via <c>GET {authApiUrl}/identities/{id}</c> and reconstructs a
        ///     fully-formed <see cref="DecentralandIdentity" />.
        /// </summary>
        private async UniTask<DecentralandIdentity> FetchIdentityByIdAsync(string identityId, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath($"identities/{identityId}"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            IdentityAuthResponseDto json = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                                     .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>();

            var authChain = AuthChain.Create();

            foreach (AuthLink authLink in json.identity.authChain)
                authChain.Set(authLink);

            string address = authChain.Get(AuthLinkType.SIGNER).payload;
            IWeb3Account ephemeralAccount = web3AccountFactory.CreateAccount(new EthECKey(json.identity.ephemeralIdentity.privateKey));
            DateTime expiration = DateTime.Parse(json.identity.expiration, null, DateTimeStyles.RoundtripKind);

            return new DecentralandIdentity(new Web3Address(address), ephemeralAccount, expiration, authChain, IWeb3Identity.Web3IdentitySource.Deeplink);
        }

        private string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

        [Serializable]
        private struct SigninRequestDto
        {
            public string method;
            public object[] @params;
        }

        [Serializable]
        private struct CreateRequestResponseDto
        {
            public string requestId;
            public string expiration;
            public int code;
        }

        [Serializable]
        private struct IdentityAuthResponseDto
        {
            public IdentityDto identity;

            [Serializable]
            public struct IdentityDto
            {
                public string expiration;
                public EphemeralIdentityDto ephemeralIdentity;
                public List<AuthLink> authChain;
            }

            [Serializable]
            public struct EphemeralIdentityDto
            {
                public string address;
                public string privateKey;
                public string publicKey;
            }
        }
    }
}
