using Cysharp.Threading.Tasks;
using DCL.Prefs;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.Signer;
using System;
using System.Threading;
using Utility.Tasks;

namespace DCL.Web3.Authenticators
{
    /// <summary>
    /// Authenticator for WebGL guest mode. Creates a persistent ephemeral Nethereum wallet:
    /// the signer private key is stored in <see cref="DCLPlayerPrefs"/> (browser localStorage on WebGL)
    /// so the user re-enters as the same guest on subsequent visits without any wallet validation.
    /// A fresh 30-day ephemeral signing account is created each session (or when the previous one expires).
    /// </summary>
    public class GuestWeb3Authenticator : IWeb3Authenticator
    {
        private const int GUEST_SESSION_DAYS = 30;

        private readonly IWeb3AccountFactory accountFactory;

        internal GuestWeb3Authenticator(IWeb3AccountFactory accountFactory)
        {
            this.accountFactory = accountFactory;
        }

        public void Dispose() { }

        public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct, IWeb3Authenticator.VerificationDelegate? codeVerificationCallback)
        {
            IWeb3Account signerAccount = LoadOrCreateSignerAccount();
            IWeb3Account ephemeralAccount = accountFactory.CreateRandomAccount();

            DateTime expiration = DateTime.UtcNow.AddDays(GUEST_SESSION_DAYS);
            string ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            string ephemeralSignature = signerAccount.Sign(ephemeralMessage);

            var authChain = AuthChain.Create();
            authChain.SetSigner(signerAccount.Address);
            authChain.Set(new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = ephemeralSignature,
            });

            DCLPlayerPrefs.SetString(DCLPrefKeys.IS_GUEST_SESSION, "true");

            return new DecentralandIdentity(
                new Web3Address(signerAccount),
                ephemeralAccount,
                expiration,
                authChain
            ).AsUniTaskResult<IWeb3Identity>();
        }

        // The signer key and IS_GUEST_SESSION flag are kept intentionally so the guest
        // returns as the same user. The ProxyWeb3Authenticator handles clearing the identity.
        public UniTask LogoutAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask<string> RequestTransferAsync(string giftUrn, string recipientAddress, CancellationToken ct) =>
            throw new NotImplementedException();

        private IWeb3Account LoadOrCreateSignerAccount()
        {
            string? storedKey = DCLPlayerPrefs.GetString(DCLPrefKeys.GUEST_SIGNER_KEY, string.Empty);

            if (!string.IsNullOrEmpty(storedKey))
                return accountFactory.CreateAccount(new EthECKey(storedKey));

            IWeb3Account newAccount = accountFactory.CreateRandomAccount();
            DCLPlayerPrefs.SetString(DCLPrefKeys.GUEST_SIGNER_KEY, newAccount.PrivateKey);
            return newAccount;
        }
    }
}
