using DCL.Utilities.Extensions;
using DCL.Web3;
using DCL.Web3.Abstract;
using DCL.Web3.Accounts;
using Nethereum.Signer;
using System;

namespace Plugins.RustEthereum.SignServerWrap
{
    public class RustEthereumAccount : IWeb3Account
    {
        private readonly IWeb3Account verifierAccount;

        public Web3Address Address { get; }

        public string PrivateKey { get; }

        public RustEthereumAccount(EthECKey key)
        {
            this.verifierAccount = NethereumAccount.CreateForVerifyOnly(key);

            PrivateKey = key.GetPrivateKey().EnsureNotNull();
            Address = new Web3Address(key.GetPublicAddress()!);
            byte[] bytes = key.GetPrivateKeyAsBytes().EnsureNotNull();
            var bytesLen = new UIntPtr((ulong)bytes.Length);

            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    bool result = NativeMethods.SignServerInitialize(ptr, bytesLen);

                    if (result == false)
                        throw new Exception("Failed to initialize sign server");
                }
            }
        }

        public string Sign(string message)
        {
            const int SIZE_OF_SIGN = 65;

            unsafe
            {
                byte* signatureBuffer = stackalloc byte[SIZE_OF_SIGN];
                NativeMethods.SignServerSignMessage(message, signatureBuffer);
                return ToHex(new Span<byte>(signatureBuffer, SIZE_OF_SIGN), true);
            }
        }

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
