using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Abstract;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
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

        private readonly IdentityByIdFetcher identityByIdFetcher;

        internal bool HasTokenFile() =>
            File.Exists(TOKEN_PATH);

        public TokenFileAuthenticator(URLAddress authApiUrl,
            IWebRequestController webRequestController,
            IWeb3AccountFactory web3AccountFactory)
        {
            identityByIdFetcher = new IdentityByIdFetcher(authApiUrl, webRequestController, web3AccountFactory);
        }

        public void Dispose() { }

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

            return await identityByIdFetcher.FetchAsync(token, IWeb3Identity.Web3IdentitySource.TokenFile, ct);
        }

        public UniTask LogoutAsync(CancellationToken ct) =>
            UniTask.CompletedTask;

        public UniTask<string> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
