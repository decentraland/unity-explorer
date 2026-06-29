using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.RuntimeDeepLink;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator
    {
        // The deep-link flow waits for the user to sign in their browser and for the OS to route the resulting
        // deep link back to this process; this can take much longer than a socket round-trip.
        private const int DEEPLINK_TIMEOUT_SECONDS = 300;

        // Deep-link sign-in collaborators. Not yet wired through the constructor: the production wiring
        // (web request controller + signin dispatcher) is the next step.
        private readonly IWebRequestController? webRequestController;
        private readonly IDeeplinkSigninDispatcher? deeplinkSigninDispatcher;

        /// <summary>
        ///     Identity-based deep-link sign-in. Same setup as <see cref="LoginAsync2" /> (mutex, socket connect, emit
        ///     "request" to obtain a requestId, open the browser) but the URL carries <c>flow=deeplink</c> and this method
        ///     does NOT subscribe to the socket "outcome" event. Instead it awaits the <see cref="deeplinkSigninDispatcher" />
        ///     for the <c>identityId</c> that the browser delivers via an OS-routed deep link, then resolves the identity
        ///     via <see cref="FetchIdentityByIdAsync" /> (GET /identities/{id}).
        ///
        ///     The browser owns the ephemeral keypair in this flow: it generates the keypair, builds the full AuthIdentity
        ///     and POSTs it to <c>/identities</c>, ignoring any ephemeral params Unity sends. The final identity is therefore
        ///     resolved entirely from the fetcher; the local ephemeral generated below is NOT used to build it. The
        ///     <c>dcl_personal_sign</c> "request" emit is still required: auth-app's RequestPage needs a <c>requestId</c> in
        ///     its URL path, and the server only mints that <c>requestId</c> in response to the emit. We keep the emit's
        ///     message byte-identical to <see cref="LoginAsync2" /> (the proven, server-accepted form).
        /// </summary>
        public async UniTask<IWeb3Identity> LoginViaDeeplinkAsync(LoginPayload payload, CancellationToken ct)
        {
            if (webRequestController == null || deeplinkSigninDispatcher == null)
                throw new Web3Exception($"{nameof(LoginViaDeeplinkAsync)} requires the web request controller and the signin dispatcher to be wired (production path only).");

            await mutex.WaitAsync(ct);

#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

            try
            {
                await UniTask.SwitchToMainThread(ct);

                await ConnectToAuthApiAsync();

                // Emit "request" exactly like LoginAsync to mint a requestId for the browser URL. The local ephemeral is
                // not used to build the final identity (the browser owns the real keypair; we resolve via the fetcher),
                // but we still send the standard well-formed message so the server accepts the emit and recover(requestId)
                // returns a valid request.
                var ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(IDENTITY_EXPIRATION_PERIOD);

                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

                SignatureIdResponse authenticationResponse = await RequestEthMethodWithSignatureAsync(new LoginAuthApiRequest
                {
                    method = "dcl_personal_sign",
                    @params = new object[] { ephemeralMessage },
                }, ct);

                await UniTask.SwitchToMainThread(ct);

                string url = $"{signatureWebAppUrl}/{authenticationResponse.requestId}?loginMethod={payload.Method}&flow=deeplink";

                webBrowser.OpenUrl(url);

                // The browser builds and stores the AuthIdentity, then opens decentraland://?signin={identityId};
                // the OS routes it to DeepLinkHandle, which dispatches it here. We do not listen to socket "outcome".
                string identityId = await WaitForSigninAsync(deeplinkSigninDispatcher, ct);

                IWeb3Identity identity = await FetchIdentityByIdAsync(webRequestController, identityId, IWeb3Identity.Web3IdentitySource.Dapp, ct);

                await DisconnectFromAuthApiAsync();

                return identity;
            }
            catch (Exception)
            {
                await DisconnectFromAuthApiAsync();
                throw;
            }
            finally
            {
#if !UNITY_WEBGL
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);
#else
                await UniTask.SwitchToMainThread(CancellationToken.None);
#endif

                mutex.Release();
            }
        }

        /// <summary>
        ///     Awaits the first <c>identityId</c> the dispatcher delivers, applying a timeout and honoring external
        ///     cancellation. Disposing the subscription on completion clears the dispatcher buffer so a deep link consumed
        ///     (or cancelled) by one attempt does not bleed into the next.
        /// </summary>
        private async UniTask<string> WaitForSigninAsync(IDeeplinkSigninDispatcher dispatcher, CancellationToken ct)
        {
            var completionSource = new UniTaskCompletionSource<string>();

            using IDisposable subscription = dispatcher.Subscribe(identityId => completionSource.TrySetResult(identityId));

            // Realtime, not the default DeltaTime: the wait spans the user switching to the browser and back, during
            // which the app is backgrounded and game time stalls. Only wall-clock measures the deadline correctly.
            return await completionSource.Task.Timeout(TimeSpan.FromSeconds(DEEPLINK_TIMEOUT_SECONDS), DelayType.Realtime).AttachExternalCancellation(ct);
        }

        /// <summary>
        ///     Fetches a stored identity by its opaque id via <c>GET {authApiUrl}/identities/{id}</c> and reconstructs a
        ///     fully-formed <see cref="DecentralandIdentity" />.
        /// </summary>
        private async UniTask<DecentralandIdentity> FetchIdentityByIdAsync(IWebRequestController requestController, string identityId, IWeb3Identity.Web3IdentitySource source, CancellationToken ct)
        {
            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath($"identities/{identityId}"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            IdentityAuthResponseDto json = await requestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                                  .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>();

            var authChain = AuthChain.Create();

            foreach (AuthLink authLink in json.identity.authChain)
                authChain.Set(authLink);

            string address = authChain.Get(AuthLinkType.SIGNER).payload;
            IWeb3Account ephemeralAccount = web3AccountFactory.CreateAccount(new EthECKey(json.identity.ephemeralIdentity.privateKey));
            DateTime expiration = DateTime.Parse(json.identity.expiration, null, DateTimeStyles.RoundtripKind);

            return new DecentralandIdentity(new Web3Address(address), ephemeralAccount, expiration, authChain, source);
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
