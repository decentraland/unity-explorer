using DCL.Web3.Abstract;
using Nethereum.Signer;
using Plugins.RustEthereum.SignServerWrap;

namespace DCL.Web3.Accounts.Factory
{
    public class Web3AccountFactory : IWeb3AccountFactory
    {
        public IWeb3Account CreateAccount(EthECKey key) =>
            new RustEthereumAccount(key);

        public IWeb3Account CreateRandomAccount()
        {
            var randomKey = EthECKey.GenerateKey()!;
            return CreateAccount(randomKey);
        }
    }
}
