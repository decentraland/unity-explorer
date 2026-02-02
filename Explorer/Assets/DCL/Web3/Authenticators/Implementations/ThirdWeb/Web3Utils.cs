using Nethereum.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using UnityEngine;

namespace DCL.Web3
{
    public static class Web3Utils
    {
        public static (string? to, string? value, string? data) ParseSendTxRequestParams(EthApiRequest request)
        {
            Dictionary<string, object>? txParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(request.@params[0].ToString());
            string? to = txParams?.TryGetValue("to", out object? toValue) == true ? toValue?.ToString() : null;
            string? value = txParams?.TryGetValue("value", out object? valueValue) == true ? valueValue?.ToString() ?? "0x0" : "0x0";
            string? data = txParams?.TryGetValue("data", out object? dataValue) == true ? dataValue?.ToString() ?? "0x" : "0x";
            return (to, value, data);
        }

        public static BigInteger ParseHexToBigInteger(string hexValue)
        {
            if (string.IsNullOrEmpty(hexValue) || hexValue == "0x" || hexValue == "0x0")
                return BigInteger.Zero;

            string hex = hexValue.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? hexValue[2..]
                : hexValue;

            return BigInteger.Parse(hex, System.Globalization.NumberStyles.HexNumber);
        }

        /// <summary>
        ///     Decodes a hex-encoded string from eth_call result.
        /// </summary>
        public static string DecodeStringFromHex(string hex)
        {
            Debug.Log($"[ThirdWeb] DecodeStringFromHex input length: {hex?.Length ?? 0}");

            if (string.IsNullOrEmpty(hex) || hex.Length < 130)
            {
                Debug.LogWarning($"[ThirdWeb] Hex too short to decode: {hex}");
                return string.Empty;
            }

            string clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            // Skip offset (32 bytes) and length (32 bytes), then read string data
            // Format: offset (32) + length (32) + data (variable)
            var lengthOffset = 64; // Skip offset
            var length = Convert.ToInt32(clean.Substring(lengthOffset, 64), 16);
            Debug.Log($"[ThirdWeb] Decoded string length: {length}");

            if (length == 0)
                return string.Empty;

            int dataOffset = lengthOffset + 64;
            int hexLength = Math.Min(length * 2, clean.Length - dataOffset);

            if (hexLength <= 0)
                return string.Empty;

            string dataHex = clean.Substring(dataOffset, hexLength);
            var bytes = new byte[dataHex.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(dataHex.Substring(i * 2, 2), 16);

            string result = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            Debug.Log($"[ThirdWeb] Decoded string result: '{result}'");
            return result;
        }

        /// <summary>
        ///     Encodes the executeMetaTransaction(address,bytes,bytes32,bytes32,uint8) call.
        ///     This is what gets sent to the transactions-server as the second param.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/utils.ts
        /// </summary>
        public static string EncodeExecuteMetaTransaction(string userAddress, string signature, string functionSignature)
        {
            // executeMetaTransaction selector = 0x0c53c51c
            const string EXECUTE_META_TX_SELECTOR = "0c53c51c";

            string sig = signature.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? signature[2..]
                : signature;

            string r = sig[..64];
            string s = sig.Substring(64, 64);
            var vInt = Convert.ToInt32(sig.Substring(128, 2), 16);

            // Normalize v value (some wallets return 0/1 instead of 27/28)
            if (vInt < 27)
                vInt += 27;

            string v = vInt.ToString("x").PadLeft(64, '0');

            Debug.Log($"[ThirdWeb] Signature components: r={r}, s={s}, v={vInt} (0x{v})");

            string method = functionSignature.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? functionSignature[2..]
                : functionSignature;

            string signatureLength = (method.Length / 2).ToString("x").PadLeft(64, '0');
            var signaturePadding = (int)Math.Ceiling(method.Length / 64.0);

            // JS library does NOT toLowerCase here - just strips 0x and pads
            string address = userAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? userAddress[2..]
                : userAddress;

            Debug.Log($"[ThirdWeb] Encoding executeMetaTransaction: address={address}, methodLen={method.Length / 2}");

            // Build the encoded call:
            // selector + address(32) + offset(32) + r(32) + s(32) + v(32) + length(32) + data(padded)
            return string.Concat(
                "0x",
                EXECUTE_META_TX_SELECTOR,
                address.PadLeft(64, '0'), // userAddress - NO toLowerCase, just like JS!
                "a0".PadLeft(64, '0'), // offset to functionSignature (160 = 0xa0)
                r, // r
                s, // s
                v, // v
                signatureLength, // length of functionSignature
                method.PadRight(64 * signaturePadding, '0') // functionSignature padded
            );
        }

        /// <summary>
        ///     Creates EIP-712 typed data JSON for meta-transaction signing.
        ///     NOTE: JS library (decentraland-transactions) does NOT lowercase addresses.
        ///     It uses account/contractAddress as-is from the wallet (usually checksum format).
        ///     EIP-712 'address' type is encoded as bytes, so case shouldn't matter for the hash.
        ///     IMPORTANT: We use explicit JSON construction to ensure exact key ordering
        ///     matches the JS library. JsonConvert with anonymous types may reorder keys.
        /// </summary>
        public static string CreateMetaTxTypedData(
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            // Salt is chainId padded to bytes32
            string salt = "0x" + chainIdValue.ToString("x").PadLeft(64, '0');

            // Build JSON manually to ensure exact key order matches JS library
            // JS object key order: types, domain, primaryType, message
            // (Note: EIP-712 shouldn't care about JSON key order, but SDK implementations might)
            var sb = new StringBuilder();
            sb.Append('{');

            // types
            sb.Append("\"types\":{");
            sb.Append("\"EIP712Domain\":[");
            sb.Append("{\"name\":\"name\",\"type\":\"string\"},");
            sb.Append("{\"name\":\"version\",\"type\":\"string\"},");
            sb.Append("{\"name\":\"verifyingContract\",\"type\":\"address\"},");
            sb.Append("{\"name\":\"salt\",\"type\":\"bytes32\"}");
            sb.Append("],");
            sb.Append("\"MetaTransaction\":[");
            sb.Append("{\"name\":\"nonce\",\"type\":\"uint256\"},");
            sb.Append("{\"name\":\"from\",\"type\":\"address\"},");
            sb.Append("{\"name\":\"functionSignature\",\"type\":\"bytes\"}");
            sb.Append("]},");

            // domain - use JsonConvert for proper escaping of contract name
            sb.Append("\"domain\":{");
            sb.Append($"\"name\":{JsonConvert.SerializeObject(contractName)},");
            sb.Append($"\"version\":{JsonConvert.SerializeObject(contractVersion)},");
            sb.Append($"\"verifyingContract\":\"{contractAddress}\",");
            sb.Append($"\"salt\":\"{salt}\"");
            sb.Append("},");

            // primaryType
            sb.Append("\"primaryType\":\"MetaTransaction\",");

            // message
            sb.Append("\"message\":{");
            sb.Append($"\"nonce\":{(long)nonce},"); // Must be a number, not string!
            sb.Append($"\"from\":\"{from}\",");
            sb.Append($"\"functionSignature\":\"{functionSignature}\"");
            sb.Append('}');

            sb.Append('}');

            var result = sb.ToString();
            Debug.Log($"[ThirdWeb] Created typed data JSON (length={result.Length}):\n{result}");
            return result;
        }

        /// <summary>
        ///     Computes EIP-712 hash manually following the exact algorithm.
        ///     Addresses are used as-is (like JS library).
        /// </summary>
        public static string ComputeEip712HashManually(
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            var sha3 = new Sha3Keccack();

            // Use addresses as-is (hex to bytes conversion is case-insensitive)

            // ========== TYPE HASHES ==========
            // EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            Debug.Log($"[EIP712-Hash] Domain type hash: 0x{BytesToHex(domainTypeHash)}");

            // MetaTransaction(uint256 nonce,address from,bytes functionSignature)
            const string META_TX_TYPE = "MetaTransaction(uint256 nonce,address from,bytes functionSignature)";
            byte[] metaTxTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(META_TX_TYPE));
            Debug.Log($"[EIP712-Hash] MetaTx type hash: 0x{BytesToHex(metaTxTypeHash)}");

            // ========== DOMAIN SEPARATOR ==========
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));
            Debug.Log($"[EIP712-Hash] Name hash ('{contractName}'): 0x{BytesToHex(nameHash)}");
            Debug.Log($"[EIP712-Hash] Version hash ('{contractVersion}'): 0x{BytesToHex(versionHash)}");

            // Salt is chainId padded to bytes32
            var salt = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainIdValue);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, salt, 32 - chainIdBytes.Length, chainIdBytes.Length);
            Debug.Log($"[EIP712-Hash] Salt (chainId={chainIdValue}): 0x{BytesToHex(salt)}");

            // verifyingContract as bytes32 (address padded to 32 bytes)
            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);
            Debug.Log($"[EIP712-Hash] Contract address padded: 0x{BytesToHex(contractAddressPadded)}");

            // Encode domain separator: abi.encode(typeHash, nameHash, versionHash, contractAddress, salt)
            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, contractAddressPadded, salt);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);
            Debug.Log($"[EIP712-Hash] ★ Our computed DOMAIN SEPARATOR: 0x{BytesToHex(domainSeparator)}");

            // ========== STRUCT HASH ==========
            // nonce as uint256 (32 bytes)
            var nonceBytes = new byte[32];
            byte[] nonceBigEndian = nonce.ToByteArray();

            if (BitConverter.IsLittleEndian && nonceBigEndian.Length > 1)
                Array.Reverse(nonceBigEndian);

            if (nonceBigEndian.Length > 0 && nonceBigEndian[0] == 0 && nonceBigEndian.Length > 1)
                nonceBigEndian = nonceBigEndian[1..];

            Array.Copy(nonceBigEndian, 0, nonceBytes, 32 - nonceBigEndian.Length, nonceBigEndian.Length);
            Debug.Log($"[EIP712-Hash] Nonce ({nonce}): 0x{BytesToHex(nonceBytes)}");

            // from address as bytes32
            byte[] fromBytes = HexToBytes(from);
            var fromPadded = new byte[32];
            Array.Copy(fromBytes, 0, fromPadded, 32 - fromBytes.Length, fromBytes.Length);
            Debug.Log($"[EIP712-Hash] From address padded: 0x{BytesToHex(fromPadded)}");

            // keccak256(functionSignature)
            byte[] funcSigBytes = HexToBytes(functionSignature);
            byte[] funcSigHash = sha3.CalculateHash(funcSigBytes);
            Debug.Log($"[EIP712-Hash] Function sig hash: 0x{BytesToHex(funcSigHash)}");

            byte[] structEncoded = ConcatBytes(metaTxTypeHash, nonceBytes, fromPadded, funcSigHash);
            byte[] structHash = sha3.CalculateHash(structEncoded);
            Debug.Log($"[EIP712-Hash] Struct hash: 0x{BytesToHex(structHash)}");

            // ========== FINAL DIGEST ==========
            var prefix = new byte[] { 0x19, 0x01 };
            byte[] digest = sha3.CalculateHash(ConcatBytes(prefix, domainSeparator, structHash));
            Debug.Log($"[EIP712-Hash] ★ Final digest: 0x{BytesToHex(digest)}");

            return "0x" + BytesToHex(digest);
        }

        /// <summary>
        ///     Computes domain separator for comparison with contract.
        ///     Uses DCL/Matic format: EIP712Domain(name,version,verifyingContract,salt)
        /// </summary>
        public static string ComputeDomainSeparator(string contractName, string contractVersion, string contractAddress, int chainId)
        {
            var sha3 = new Sha3Keccack();

            // DCL/Matic format with salt
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,address verifyingContract,bytes32 salt)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));

            var salt = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, salt, 32 - chainIdBytes.Length, chainIdBytes.Length);

            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);

            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, contractAddressPadded, salt);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        /// <summary>
        ///     Computes domain separator using STANDARD EIP-712 format with chainId (not salt).
        ///     Format: EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)
        /// </summary>
        public static string ComputeDomainSeparatorStandard(string contractName, string contractVersion, string contractAddress, int chainId)
        {
            var sha3 = new Sha3Keccack();

            // Standard EIP-712 format with chainId (uint256)
            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,uint256 chainId,address verifyingContract)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            Debug.Log($"[DomainSep-Std] Type hash: 0x{BytesToHex(domainTypeHash)}");

            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));
            Debug.Log($"[DomainSep-Std] Name hash ('{contractName}'): 0x{BytesToHex(nameHash)}");
            Debug.Log($"[DomainSep-Std] Version hash ('{contractVersion}'): 0x{BytesToHex(versionHash)}");

            // chainId as uint256 (32 bytes, big-endian)
            var chainIdPadded = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, chainIdPadded, 32 - chainIdBytes.Length, chainIdBytes.Length);
            Debug.Log($"[DomainSep-Std] ChainId ({chainId}): 0x{BytesToHex(chainIdPadded)}");

            // verifyingContract as address (20 bytes padded to 32)
            byte[] contractAddressBytes = HexToBytes(contractAddress);
            var contractAddressPadded = new byte[32];
            Array.Copy(contractAddressBytes, 0, contractAddressPadded, 32 - contractAddressBytes.Length, contractAddressBytes.Length);
            Debug.Log($"[DomainSep-Std] Contract: 0x{BytesToHex(contractAddressPadded)}");

            // Order: typeHash, nameHash, versionHash, chainId, verifyingContract
            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, chainIdPadded, contractAddressPadded);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        /// <summary>
        ///     Computes domain separator with MINIMAL fields (name, version, chainId only).
        ///     Some contracts use this simpler format.
        /// </summary>
        public static string ComputeDomainSeparatorMinimal(string contractName, string contractVersion, int chainId)
        {
            var sha3 = new Sha3Keccack();

            const string DOMAIN_TYPE = "EIP712Domain(string name,string version,uint256 chainId)";
            byte[] domainTypeHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(DOMAIN_TYPE));
            byte[] nameHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractName));
            byte[] versionHash = sha3.CalculateHash(Encoding.UTF8.GetBytes(contractVersion));

            var chainIdPadded = new byte[32];
            byte[] chainIdBytes = BitConverter.GetBytes(chainId);

            if (BitConverter.IsLittleEndian)
                Array.Reverse(chainIdBytes);

            Array.Copy(chainIdBytes, 0, chainIdPadded, 32 - chainIdBytes.Length, chainIdBytes.Length);

            byte[] domainEncoded = ConcatBytes(domainTypeHash, nameHash, versionHash, chainIdPadded);
            byte[] domainSeparator = sha3.CalculateHash(domainEncoded);

            return "0x" + BytesToHex(domainSeparator);
        }

        public static byte[] HexToBytes(string hex)
        {
            string clean = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? hex[2..] : hex;

            if (clean.Length % 2 != 0)
                clean = "0" + clean;

            var bytes = new byte[clean.Length / 2];

            for (var i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(clean.Substring(i * 2, 2), 16);

            return bytes;
        }

        public static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
                sb.AppendFormat("{0:x2}", b);

            return sb.ToString();
        }

        public static byte[] ConcatBytes(params byte[][] arrays)
        {
            var totalLength = 0;

            foreach (byte[] arr in arrays)
                totalLength += arr.Length;

            var result = new byte[totalLength];
            var offset = 0;

            foreach (byte[] arr in arrays)
            {
                Array.Copy(arr, 0, result, offset, arr.Length);
                offset += arr.Length;
            }

            return result;
        }
    }
}
