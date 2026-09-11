using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility;
using DCL.Utility.Types;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Nethereum.Signer;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class TokenFileAuthenticator : IWeb3Authenticator
    {
        private static readonly string TOKEN_PATH = LauncherPaths.InLauncherDirectory("auth-token-bridge.txt");

        private readonly URLAddress authApiUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly URLBuilder urlBuilder = new ();

        internal bool HasTokenFile() =>
            File.Exists(TOKEN_PATH);

        public TokenFileAuthenticator(URLAddress authApiUrl,
            IWebRequestController webRequestController,
            IWeb3AccountFactory web3AccountFactory)
        {
            this.authApiUrl = authApiUrl;
            this.webRequestController = webRequestController;
            this.web3AccountFactory = web3AccountFactory;
        }

        public void Dispose()
        {
        }

        public async UniTask<IWeb3Identity> LoginAsync(LoginPayload _, CancellationToken ct) =>
            await LoginAsync(ct);

        private async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            if (!File.Exists(TOKEN_PATH))
                throw new AutoLoginTokenNotFoundException();

            Result<string> contentResult = await File.ReadAllTextAsync(TOKEN_PATH, ct)!.SuppressToResultAsync<string>(ReportCategory.AUTHENTICATION);

            if (contentResult.Success == false)
                throw new Exception(contentResult.ErrorMessage ?? "Cannot read token file");

            // Notify emitter that the file has been consumed
            File.Delete(TOKEN_PATH);

            string token = contentResult.Value.Trim();

            if (!Guid.TryParse(token, out _))
                throw new AutoLoginTokenInvalidException($"Token read from {TOKEN_PATH} is invalid. {token}");

            urlBuilder.Clear();

            urlBuilder.AppendDomain(URLDomain.FromString(authApiUrl))
                      .AppendPath(new URLPath($"identities/{token}"));

            var commonArguments = new CommonArguments(urlBuilder.Build());

            IdentityAuthResponseDto json = await webRequestController.GetAsync(commonArguments, ct, ReportCategory.AUTHENTICATION)
                                                 .CreateFromNewtonsoftJsonAsync<IdentityAuthResponseDto>();

            var authChain = AuthChain.Create();
            foreach (AuthLink authLink in json.identity.authChain)
                authChain.Set(authLink);
            string address = authChain.Get(AuthLinkType.SIGNER).payload;
            IWeb3Account ephemeralAccount = web3AccountFactory.CreateAccount(new EthECKey(json.identity.ephemeralIdentity.privateKey));
            DateTime expiration = DateTime.Parse(json.identity.expiration, null, DateTimeStyles.RoundtripKind);

            return new DecentralandIdentity(new Web3Address(address), ephemeralAccount, expiration, authChain,
                LoginMethod.TOKEN_FILE);
        }

        public UniTask<string> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
