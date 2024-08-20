using Nethereum.Signer;

namespace DCL.Web3.Abstract
{
    public interface IEthKeyOwner
    {
        EthECKey Key { get; }
    }
}
