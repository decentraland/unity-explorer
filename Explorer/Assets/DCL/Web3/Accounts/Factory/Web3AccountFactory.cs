using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using Nethereum.Signer;
#if !UNITY_WEBGL
using Plugins.RustEthereum.SignServerWrap;
#endif

namespace DCL.Web3.Accounts.Factory
{
    public class Web3AccountFactory : IWeb3AccountFactory
    {
        public IWeb3Account CreateAccount(EthECKey key)
        {
#if UNITY_WEBGL
            // WebGL doesn't support native Rust libraries, use Nethereum instead
            return NethereumAccount.Create(key);
#else
            return new RustEthereumAccount(key);
#endif
        }

        public IWeb3Account CreateRandomAccount()
        {
            var randomKey = EthECKey.GenerateKey()!;
            return CreateAccount(randomKey);
        }
    }
}
