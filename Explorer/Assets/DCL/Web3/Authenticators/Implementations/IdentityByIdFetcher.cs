using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using System;
using System.Globalization;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    ///     Fetches a stored identity by its opaque id via <c>GET {authApiUrl}/identities/{id}</c> and reconstructs
    ///     a fully-formed <see cref="DecentralandIdentity" />. Shared by the launcher auto-login
    ///     (<see cref="TokenFileAuthenticator" />) and the deep-link sign-in flow.
    /// </summary>
    public class IdentityByIdFetcher
    {
        private readonly URLAddress authApiUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly URLBuilder urlBuilder = new ();

        public IdentityByIdFetcher(URLAddress authApiUrl,
            IWebRequestController webRequestController,
            IWeb3AccountFactory web3AccountFactory)
        {
            this.authApiUrl = authApiUrl;
            this.webRequestController = webRequestController;
            this.web3AccountFactory = web3AccountFactory;
        }

        public async UniTask<DecentralandIdentity> FetchAsync(string identityId, IWeb3Identity.Web3IdentitySource source, CancellationToken ct)
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

            return new DecentralandIdentity(new Web3Address(address), ephemeralAccount, expiration, authChain, source);
        }
    }
}
