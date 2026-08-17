using DCL.RustEthereum;
using DCL.Utilities.Extensions;
using DCL.Web3.Abstract;
using Nethereum.Signer;
using System;

namespace DCL.Web3.Accounts
{
    public class RustEthereumAccount : IWeb3Account
    {
        private const int PRIVATE_KEY_SIZE = 32;

        private readonly IWeb3Account verifierAccount;

        public Web3Address Address { get; }

        public string PrivateKey { get; }

        public RustEthereumAccount(EthECKey key)
        {
            verifierAccount = NethereumAccount.CreateForVerifyOnly(key);

            Address = new Web3Address(key.GetPublicAddress()!);

            // secp256k1 private keys are 32-byte big-endian scalars; Nethereum's
            // GetPrivateKeyAsBytes() returns the scalar unsigned-trimmed, without leading
            // zero bytes, while RustEthSignServer.Initialize demands exactly 32 bytes.
            byte[] bytes = LeftPad(key.GetPrivateKeyAsBytes().EnsureNotNull(), PRIVATE_KEY_SIZE);
            PrivateKey = ToHex(bytes, true);

            if (!RustEthSignServer.Initialize(bytes))
                throw new Exception("Failed to initialize sign server");
        }

        private static byte[] LeftPad(byte[] value, int size)
        {
            if (value.Length >= size)
                return value;

            // Fresh array: the input is cached inside EthECKey and must not be mutated.
            var padded = new byte[size];
            Buffer.BlockCopy(value, 0, padded, size - value.Length, value.Length);
            return padded;
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
