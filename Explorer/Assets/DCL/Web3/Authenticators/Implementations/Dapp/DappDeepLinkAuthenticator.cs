using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Utilities;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Production wallet sign-in via browser and OS deep link: generates a client-side auth request id,
    ///     opens the browser on the signature web app for the user to sign with their wallet, then awaits the
    ///     deep link that carries the resulting identity id (matched against that request id) and resolves the
    ///     identity from the auth server.
    /// </summary>
    public class DappDeepLinkAuthenticator : IWeb3Authenticator
    {
        private const int DEEPLINK_TIMEOUT_SECONDS = 300;

        private readonly UnityAppWebBrowser webBrowser;
        private readonly URLAddress authApiUrl;
        private readonly URLAddress signatureWebAppUrl;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly IWebRequestController webRequestController;
        private readonly ReactiveProperty<string?> deeplinkSigninIdentityId;
        private readonly ReactiveProperty<string?> loginAwaitingSigninRequestId;
        private readonly Web3Address? referrer;
        private readonly URLBuilder urlBuilder = new ();
        private readonly DCLSemaphoreSlim loginMutex = new ();

        public DappDeepLinkAuthenticator(
            UnityAppWebBrowser webBrowser,
            URLAddress authApiUrl,
            URLAddress signatureWebAppUrl,
            IWeb3AccountFactory web3AccountFactory,
            IWebRequestController webRequestController,
            ReactiveProperty<string?> deeplinkSigninIdentityId,
            ReactiveProperty<string?> loginAwaitingSigninRequestId,
            string? referrer = null)
        {
            this.webBrowser = webBrowser;
            this.authApiUrl = authApiUrl;
            this.signatureWebAppUrl = signatureWebAppUrl;
            this.web3AccountFactory = web3AccountFactory;
            this.webRequestController = webRequestController;
            this.deeplinkSigninIdentityId = deeplinkSigninIdentityId;
            this.loginAwaitingSigninRequestId = loginAwaitingSigninRequestId;
            // Normalized/validated once at construction so the field is always canonical;
            // an invalid launch-argument value degrades to "no referrer".
            this.referrer = Web3Address.FromUntrusted(referrer);
        }

        public void Dispose()
        {
            loginMutex.Dispose();
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload payload, CancellationToken ct)
        {
            await loginMutex.WaitAsync(ct);

            try
            {
                // A signin id stored before this attempt minted its request cannot belong to it: drop it
                // so a stale or foreign deep link does not complete this login with another session's identity.
                deeplinkSigninIdentityId.Value = null;

                await UniTask.SwitchToMainThread(ct);

                // Client-generated id embedded in the browser URL; no server round-trip needed before opening the browser.
                var authRequestId = Guid.NewGuid().ToString();
#if UNITY_EDITOR

                // Without this flag the auth website also launches a standalone Explorer build,
                // which would steal the signin from the editor.
                const bool BRIDGE_ONLY = true;
#else
                const bool BRIDGE_ONLY = false;
#endif
                string url = DeepLinkSignInUrl.Build(signatureWebAppUrl, authRequestId, payload.Method.ToString(), BRIDGE_ONLY, referrer);

                webBrowser.OpenUrlMainThreadOnly(url);

                // Resolves when the OS delivers the deep link that carries the identity id.
                string identityId = await WaitForSigninAsync(authRequestId, ct);

                return await FetchIdentityByIdAsync(identityId, ct);
            }
            finally { loginMutex.Release(); }
        }

        /// <summary>
        ///     Awaits the first non-empty <c>identityId</c>, starting from the currently stored one, and consumes it.
        /// </summary>
        private async UniTask<string> WaitForSigninAsync(string requestId, CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<string>();

            // Publishes the request id awaited by the pipeline.
            loginAwaitingSigninRequestId.Value = requestId;

            using var subscription = deeplinkSigninIdentityId.UseCurrentValueAndSubscribeToUpdate(completionSource,
                static (identityId, completion) =>
                {
                    if (!string.IsNullOrEmpty(identityId))
                        completion.TrySetResult(identityId);
                }, ct);

            try { return await completionSource.Task.Timeout(TimeSpan.FromSeconds(DEEPLINK_TIMEOUT_SECONDS), DelayType.Realtime).AttachExternalCancellation(ct); }
            finally
            {
                // Stop accepting signins and consume the id on success, timeout and cancellation alike, so a
                // later login attempt never resolves against a signin delivered for this one.
                loginAwaitingSigninRequestId.Value = null;
                deeplinkSigninIdentityId.Value = null;
            }
        }

        private async UniTask<DecentralandIdentity> FetchIdentityByIdAsync(string identityId, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath($"identities/{identityId}"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            IdentityAuthResponseDto json = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                                     .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>()
                                                                     .WithCustomExceptionAsync(e => e.ResponseCode switch
                                                                                                    {
                                                                                                        404 => new DeeplinkSigninRetrievalException(DeeplinkSigninRetrievalException.ErrorReason.NotFound, identityId),
                                                                                                        410 => new DeeplinkSigninRetrievalException(DeeplinkSigninRetrievalException.ErrorReason.Expired, identityId),
                                                                                                        403 => new DeeplinkSigninRetrievalException(DeeplinkSigninRetrievalException.ErrorReason.IpMismatch, identityId),
                                                                                                        _ => e,
                                                                                                    });

            string? signerAddress = null;
            string? ephemeralPayload = null;

            foreach (AuthLink authLink in json.identity.authChain)
                if (authLink.type == AuthLinkType.SIGNER)
                    signerAddress = authLink.payload;
                else if (authLink.type is AuthLinkType.ECDSA_EPHEMERAL or AuthLinkType.ECDSA_EIP_1654_EPHEMERAL)
                    ephemeralPayload = authLink.payload;

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

        // Field names mirror the auth server's JSON payloads verbatim, so they intentionally break the naming rules.
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
