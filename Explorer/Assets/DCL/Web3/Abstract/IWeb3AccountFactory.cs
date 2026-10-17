using Nethereum.Signer;

namespace DCL.Web3.Abstract
{
    public interface IWeb3AccountFactory
    {
        IWeb3Account CreateAccount(EthECKey key);

        IWeb3Account CreateRandomAccount();
    }
}
