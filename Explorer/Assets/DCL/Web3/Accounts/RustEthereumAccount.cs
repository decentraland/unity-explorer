using DCL.RustEthereum;
using DCL.Utilities.Extensions;
using DCL.Web3.Abstract;
using Nethereum.Signer;
using System;

namespace DCL.Web3.Accounts
{
    public class RustEthereumAccount : IWeb3Account
    {
        private readonly IWeb3Account verifierAccount;

        public Web3Address Address { get; }

        public string PrivateKey { get; }

        public RustEthereumAccount(EthECKey key)
        {
            verifierAccount = NethereumAccount.CreateForVerifyOnly(key);

            PrivateKey = key.GetPrivateKey().EnsureNotNull();
            Address = new Web3Address(key.GetPublicAddress()!);
            byte[] bytes = key.GetPrivateKeyAsBytes().EnsureNotNull();

            if (!RustEthSignServer.Initialize(bytes))
                throw new Exception("Failed to initialize sign server");
        }

        public string Sign(string message) =>
            ToHex(RustEthSignServer.Sign(message), true);

        public bool Verify(string message, string signature) =>
            verifierAccount.Verify(message, signature);

        public static string ToHex(ReadOnlySpan<byte> value, bool prefix = false)
        {
            string currentPrefix = prefix ? "0x" : "";
            var buffer = new string[value.Length];
            for (var i = 0; i < value.Length; i++) buffer[i] = value[i].ToString("x2");
            return currentPrefix + string.Concat(buffer);
        }
    }
}
