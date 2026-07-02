using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using JetBrains.Annotations;
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
    ///     opens the browser for the user to sign with their wallet, then awaits the deep link identity id
    ///     and resolves the resulting identity from the server.
    /// </summary>
    public class DappDeepLinkAuthenticator : IWeb3Authenticator
    {
        private const double IDENTITY_EXPIRATION_PERIOD_FALLBACK_IN_DAYS = 30;
        private const int DEEPLINK_TIMEOUT_SECONDS = 300;

        private readonly UnityAppWebBrowser webBrowser;
        private readonly URLAddress authApiUrl;
        private readonly URLAddress signatureWebAppUrl;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly IWebRequestController webRequestController;
        private readonly ReactiveProperty<string?> deeplinkSigninIdentityId;
        private readonly int? identityExpirationDuration;
        private readonly URLBuilder urlBuilder = new ();

        public DappDeepLinkAuthenticator(
            UnityAppWebBrowser webBrowser,
            URLAddress authApiUrl,
            URLAddress signatureWebAppUrl,
            IWeb3AccountFactory web3AccountFactory,
            IWebRequestController webRequestController,
            ReactiveProperty<string?> deeplinkSigninIdentityId,
            int? identityExpirationDuration = null)
        {
            this.webBrowser = webBrowser;
            this.authApiUrl = authApiUrl;
            this.signatureWebAppUrl = signatureWebAppUrl;
            this.web3AccountFactory = web3AccountFactory;
            this.webRequestController = webRequestController;
            this.deeplinkSigninIdentityId = deeplinkSigninIdentityId;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public void Dispose() { }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            // The ephemeral address is embedded in the signed message so the server can mint a well-formed request from it.
            var ephemeralAccount = web3AccountFactory.CreateRandomAccount();

            DateTime sessionExpiration = identityExpirationDuration != null
                ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                : DateTime.UtcNow.AddDays(IDENTITY_EXPIRATION_PERIOD_FALLBACK_IN_DAYS);

            string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

            CreateRequestResponseDto createRequestResponse = await CreateSigninRequestAsync(ephemeralMessage, ct);

            if (string.IsNullOrEmpty(createRequestResponse.requestId))
                throw new Web3Exception("Cannot solve auth request id");

            await UniTask.SwitchToMainThread(ct);

            string url = $"{signatureWebAppUrl}/{createRequestResponse.requestId}?loginMethod={payload.Method}&flow=deeplink";

            webBrowser.OpenUrlMainThreadOnly(url);

            // The browser builds and stores the AuthIdentity, then opens decentraland://?signin={identityId},
            // which is delivered here through the deep link pipeline.
            string identityId = await WaitForSigninAsync(ct);

            return await FetchIdentityByIdAsync(identityId, ct);
        }

        /// <summary>
        ///     Mints a sign-in <c>requestId</c> via <c>POST {authApiUrl}/requests</c>.
        ///     The browser later recovers the request by that id to drive the wallet signature.
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
        ///     Awaits the first non-empty <c>identityId</c>, starting from the currently stored one, and consumes it.
        /// </summary>
        private async UniTask<string> WaitForSigninAsync(CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<string>();

            using var subscription = deeplinkSigninIdentityId.UseCurrentValueAndSubscribeToUpdate(completionSource,
                static (identityId, completion) =>
                {
                    if (!string.IsNullOrEmpty(identityId))
                        completion.TrySetResult(identityId);
                }, ct);

            string identityId = await completionSource.Task.Timeout(TimeSpan.FromSeconds(DEEPLINK_TIMEOUT_SECONDS), DelayType.Realtime).AttachExternalCancellation(ct);

            // Consume the id so a future login does not resolve against this same signin.
            deeplinkSigninIdentityId.Value = null;

            return identityId;
        }

        private async UniTask<DecentralandIdentity> FetchIdentityByIdAsync(string identityId, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath($"identities/{identityId}"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            IdentityAuthResponseDto json = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                                     .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>();

            string? signerAddress = null;
            string? ephemeralPayload = null;

            foreach (AuthLink authLink in json.identity.authChain)
            {
                if (authLink.type == AuthLinkType.SIGNER)
                    signerAddress = authLink.payload;
                else if (authLink.type is AuthLinkType.ECDSA_EPHEMERAL or AuthLinkType.ECDSA_EIP_1654_EPHEMERAL)
                    ephemeralPayload = authLink.payload;
            }

            if (signerAddress is not { Length: > 0 })
                throw new Web3Exception($"Sign-in identity {identityId} has no SIGNER link in its auth chain");

            if (ephemeralPayload is not { Length: > 0 })
                throw new Web3Exception($"Sign-in identity {identityId} has no ephemeral link in its auth chain");

            IWeb3Account ephemeralAccount = web3AccountFactory.CreateAccount(new EthECKey(json.identity.ephemeralIdentity.privateKey));

            // Every signed request is made with this ephemeral key: if it does not match the address the wallet
            // signed into the ephemeral link, servers reject every signature. Fail fast at login instead.
            if (!ephemeralPayload.Contains(ephemeralAccount.Address, StringComparison.OrdinalIgnoreCase))
                throw new Web3Exception($"Sign-in identity {identityId} is inconsistent: the ephemeral private key does not match the auth chain ephemeral address {ephemeralAccount.Address}");

            var authChain = AuthChain.Create();

            foreach (AuthLink authLink in json.identity.authChain)
                authChain.Set(authLink);

            DateTime expiration = DateTime.Parse(json.identity.expiration, null, DateTimeStyles.RoundtripKind);

            return new DecentralandIdentity(new Web3Address(signerAddress), ephemeralAccount, expiration, authChain, IWeb3Identity.Web3IdentitySource.Deeplink);
        }

        private static string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

        [Serializable]
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
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
