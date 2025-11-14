using System;
using System.Numerics;

namespace DCL.Backpack.Gifting.Services
{
    public static class ManualTxEncoder
    {
        // keccak256("transferFrom(address,address,uint256)")[:4]
        private const string SELECTOR = "23b872dd";

        public static string EncodeTransferFrom(string from, string to, string tokenIdDec)
        {
            // 1) normalize inputs
            string aFrom = NormalizeAddress(from);
            string aTo   = NormalizeAddress(to);
            string idHex = BigInteger.Parse(tokenIdDec).ToString("x"); // base-16

            // 2) left-pad to 32 bytes (64 hex nibbles)
            string wordFrom = LeftPad64(aFrom);
            string wordTo   = LeftPad64(aTo);
            string wordId   = LeftPad64(idHex);

            // 3) concat: 0x + selector + args
            return "0x" + SELECTOR + wordFrom + wordTo + wordId;
        }

        private static string NormalizeAddress(string addr)
        {
            string s = addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? addr[2..] : addr;
            if (s.Length != 40) throw new ArgumentException("Invalid address length");
            return s.ToLowerInvariant();
        }

        private static string LeftPad64(string hex)
        {
            string s = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;
            if (s.Length > 64) throw new ArgumentException("Too long");
            return s.PadLeft(64, '0').ToLowerInvariant();
        }
    }
}