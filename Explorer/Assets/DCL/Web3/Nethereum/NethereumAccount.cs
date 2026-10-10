using DCL.Web3.Abstract;
using Nethereum.Signer;

namespace DCL.Web3.Accounts
{
    public class NethereumAccount : IWeb3Account
    {
        private static readonly EthereumMessageSigner SIGNER = new ();
        internal readonly EthECKey key;

        public Web3Address Address { get; }

        public string PrivateKey => key.GetPrivateKey()!;

        private NethereumAccount(EthECKey key)
        {
            this.key = key;
            Address = new Web3Address(key.GetPublicAddress());
        }

        public static NethereumAccount CreateForVerifyOnly(EthECKey key) =>
            new (key);

        public string Sign(string message) =>
            SIGNER.EncodeUTF8AndSign(message, key);

        public bool Verify(string message, string signature) =>
            SIGNER.EncodeUTF8AndEcRecover(message, signature) == Address;
    }
}
