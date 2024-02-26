using Nethereum.Signer;

namespace DCL.Web3.Accounts
{
    public interface IEthKeyOwner
    {
        EthECKey Key { get; }
    }
}
