using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer;
using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class TokenFileAuthenticator : IWeb3Authenticator
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || PLATFORM_STANDALONE_WIN
        // path for: C:\Users\<YourUsername>\AppData\Local\DecentralandLauncherLight\
        private static readonly string TOKEN_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DecentralandLauncherLight", "auth-token-bridge.txt"
            );
#else
        // path for: ~/Library/Application Support/DecentralandLauncherLight/
        private static readonly string TOKEN_PATH =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library", "Application Support", "DecentralandLauncherLight", "auth-token-bridge.txt"
            );
#endif

        private readonly URLAddress authApiUrl;
        private readonly IWebRequestController webRequestController;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly URLBuilder urlBuilder = new ();

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

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            if (!File.Exists(TOKEN_PATH))
                throw new AutoLoginTokenNotFoundException();

            Result<string> contentResult = await File.ReadAllTextAsync(TOKEN_PATH, ct)!.SuppressToResultAsync<string>(ReportCategory.AUTHENTICATION);

            if (contentResult.Success == false)
                throw new Exception(contentResult.ErrorMessage ?? "Cannot read token file");

            // Notify emitter that the file has been consumed
            File.Delete(TOKEN_PATH);

            string token = contentResult.Value;

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

            DateTime expiration = DateTime.ParseExact(json.identity.expiration, "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

            return new DecentralandIdentity(new Web3Address(address), ephemeralAccount, expiration, authChain);
        }

        public UniTask LogoutAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;
    }
}
