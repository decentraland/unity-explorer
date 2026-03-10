using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Threading;
using Thirdweb;

namespace DCL.Web3.Authenticators
{
    public class ThirdWebMetaTxService
    {
        private const string HEX_CHARS = "0123456789abcdef";
        private const int ABI_WORD_HEX = 64; // 32 bytes = 64 hex chars

        /// <summary>
        ///     Pre-built constant for the EIP-712 types section (identical for every meta-tx).
        /// </summary>
        private const string TYPED_DATA_TYPES_JSON =
            "{\"types\":{\"EIP712Domain\":["
            + "{\"name\":\"name\",\"type\":\"string\"},"
            + "{\"name\":\"version\",\"type\":\"string\"},"
            + "{\"name\":\"verifyingContract\",\"type\":\"address\"},"
            + "{\"name\":\"salt\",\"type\":\"bytes32\"}"
            + "],\"MetaTransaction\":["
            + "{\"name\":\"nonce\",\"type\":\"uint256\"},"
            + "{\"name\":\"from\",\"type\":\"address\"},"
            + "{\"name\":\"functionSignature\",\"type\":\"bytes\"}"
            + "]},";

        // Fixed JSON structure size around variable parts in CreateMetaTxTypedData:
        //   "domain":{"name":                 17
        //   ,"version":                       11
        //   ,"verifyingContract":"             22
        //   ","salt":"0x                       12
        //   "},                                 3
        //   "primaryType":"MetaTransaction",   32
        //   "message":{"nonce":                19
        //   ,"from":"                           9
        //   ","functionSignature":"             23
        //   "}}                                  3
        //                               Total: 151
        private const int TYPED_DATA_FIXED_OVERHEAD = 151;

        // Fixed JSON structure size in PostToTransactionsServerAsync payload:
        //   {"transactionData":{"from":"       28
        //   ","params":["                      13
        //   ","                                 3
        //   "]}}                                 4
        //                               Total: 48
        private const int POST_PAYLOAD_FIXED_OVERHEAD = 48;

        /// <summary>
        ///     Known Decentraland contracts that support meta-transactions.
        ///     Key = contract address (case-insensitive), Value = (name, version) for EIP-712 domain.
        ///     IMPORTANT: EIP-712 domain names must EXACTLY match what's in decentraland-transactions npm package!
        ///     See: https://github.com/decentraland/decentraland-transactions/blob/master/src/contracts/manaToken.ts
        ///     See dApp implementation: https://github.com/decentraland/auth/blob/main/src/components/Pages/RequestPage/RequestPage.tsx#L258
        /// </summary>
        private static readonly Dictionary<string, ContractMetaTxInfo> KNOWN_META_TX_CONTRACTS = new (StringComparer.OrdinalIgnoreCase)
        {
            { "0xa1c57f48f0deb89f569dfbe6e2b7f46d33606fd4", new ContractMetaTxInfo("(PoS) Decentraland MANA", "1") }, // MANA on Polygon Mainnet
            { "0x7ad72b9f944ea9793cf4055d88f81138cc2c63a0", new ContractMetaTxInfo("Decentraland MANA(PoS)", "1", 80002) }, // MANA on Polygon Amoy Testnet (for testing ThirdWeb donation flow)
            { "0x480a0f4e360e8964e68858dd231c2922f1df45ef", new ContractMetaTxInfo("Decentraland Marketplace", "2") }, // Marketplace V2 on Polygon
            { "0xb96697fa4a3361ba35b774a42c58daccaad1b8e1", new ContractMetaTxInfo("Decentraland Bid", "2") }, // Bids V2 on Polygon
            { "0x9d32aac179153a991e832550d9f96f6d1e05d4b4", new ContractMetaTxInfo("CollectionManager", "2") }, // Collection Manager on Polygon
        };

        /// <summary>
        ///     Default EIP-712 domain for unknown contracts (all DCL wearable/emote collection contracts).
        ///     Cached to avoid allocation on each unknown-contract lookup.
        /// </summary>
        private static readonly ContractMetaTxInfo DEFAULT_COLLECTION_INFO = new ("Decentraland Collection", "2");

        private readonly ThirdwebClient client;
        private readonly URLDomain metaTxServerUrl;
        private readonly Func<EthApiRequest, int, UniTask<EthApiResponse>> sendRpcRequest;

        public ThirdWebMetaTxService(ThirdwebClient client, URLDomain metaTxServerUrl, Func<EthApiRequest, int, UniTask<EthApiResponse>> sendRpcRequest)
        {
            this.client = client;
            this.metaTxServerUrl = metaTxServerUrl;
            this.sendRpcRequest = sendRpcRequest;
        }

        /// <summary>
        ///     Sends a meta-transaction via Decentraland's transactions-server.
        ///     The user signs an EIP-712 message, and the server relays the transaction paying for gas.
        /// </summary>
        public async UniTask<string> SendMetaTransactionAsync(IThirdwebWallet wallet, string contractAddress, string functionSignature)
        {
            string from = await wallet!.GetAddress();

            // Get contract data for EIP-712 domain (includes chainId for the contract's network)
            ContractMetaTxInfo contractInfo = await GetContractMetaTxInfoAsync(contractAddress);
            ReportHub.Log(ReportCategory.AUTHENTICATION, $"Contract info - Name: {contractInfo.Name}, Version: {contractInfo.Version}, ChainId: {contractInfo.ChainId}");

            BigInteger nonce = await GetMetaTxNonceAsync(contractAddress, from, contractInfo.ChainId);

            // Manual EIP-712: compute hash with Nethereum and sign via eth_sign (raw hash signing)
            string signature = await SignMetaTxManuallyAsync(
                wallet,
                contractInfo.Name,
                contractInfo.Version,
                contractAddress,
                contractInfo.ChainId,
                nonce,
                from,
                functionSignature
            );

            string txData = EncodeExecuteMetaTransaction(from, signature, functionSignature);
            return await PostToTransactionsServerAsync(from, contractAddress, txData);
        }

        /// <summary>
        ///     Manually computes EIP-712 hash using Nethereum and verifies/debugs the signing process.
        ///     Can either sign via ThirdWeb SignTypedDataV4 or sign the raw hash directly.
        /// </summary>
        private async UniTask<string> SignMetaTxManuallyAsync(
            IThirdwebWallet wallet,
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            // Create TypedData using Nethereum EIP712
            // Note: DCL uses 'salt' instead of 'chainId' in domain (bytes32 vs uint256)
            string typedDataJson = CreateMetaTxTypedData(
                contractName,
                contractVersion,
                contractAddress,
                chainIdValue,
                nonce,
                from,
                functionSignature
            );

            return await wallet!.SignTypedDataV4(typedDataJson);
        }

        /// <summary>
        ///     Gets the meta-transaction nonce for a user from the contract.
        ///     Calls getNonce(address) on the contract.
        /// </summary>
        private async UniTask<BigInteger> GetMetaTxNonceAsync(string contractAddress, string userAddress, int targetChainId)
        {
            // getNonce(address) selector = keccak256("getNonce(address)")[:4] = 0x2d0335ab
            // Build "0x2d0335ab" + lowercase address padded to 64 hex chars = 74 chars total
            // Single allocation via string.Create instead of ToLower() + PadLeft() + concat
            string data = string.Create(74, userAddress, static (span, addr) =>
            {
                "0x2d0335ab".AsSpan().CopyTo(span);

                Span<char> dest = span[10..];
                dest.Fill('0');

                ReadOnlySpan<char> a = addr.AsSpan();

                if (a.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    a = a[2..];

                int padOffset = ABI_WORD_HEX - a.Length;

                for (int i = 0; i < a.Length; i++)
                    dest[padOffset + i] = char.ToLowerInvariant(a[i]);
            });

            var request = new EthApiRequest
            {
                id = Guid.NewGuid().GetHashCode(),
                method = "eth_call",
                @params = new object[]
                {
                    new { to = contractAddress, data },
                    "latest",
                },
            };

            EthApiResponse response = await sendRpcRequest(request, targetChainId);
            return Web3Utils.ParseHexToBigInteger(response.result?.ToString() ?? "0x0");
        }

        /// <summary>
        ///     Gets contract metadata needed for EIP-712 domain.
        ///     First tries known contracts (MANA, Marketplace, etc.), then uses DCL collection defaults.
        ///
        /// For DCL wearable/emote collection contracts (ERC721BaseCollectionV2):
        /// EIP-712 domain is HARDCODED in the contract, NOT from name() function!
        /// See: ERC721BaseCollectionV2.initialize() calls _initializeEIP712('Decentraland Collection', '2')
        /// The name() function returns the ERC721 token name, which is DIFFERENT from EIP-712 domain name.
        ///
        /// All DCL collection contracts use:
        ///   - EIP712 name: "Decentraland Collection"
        ///   - EIP712 version: "2"
        ///   - ChainId: 137 (Polygon mainnet - DCL collections are only on Polygon mainnet)
        /// </summary>
        private UniTask<ContractMetaTxInfo> GetContractMetaTxInfoAsync(string contractAddress)
        {
            // OrdinalIgnoreCase comparer on dictionary eliminates ToLower() allocation
            if (KNOWN_META_TX_CONTRACTS.TryGetValue(contractAddress, out ContractMetaTxInfo? known))
                return UniTask.FromResult(known);

            ReportHub.LogWarning(ReportCategory.AUTHENTICATION, $"Contract NOT found in {nameof(KNOWN_META_TX_CONTRACTS)}. Using DEFAULT DCL collection EIP-712 domain");
            return UniTask.FromResult(DEFAULT_COLLECTION_INFO);
        }

        /// <summary>
        ///     POSTs the signed meta-transaction to Decentraland's transactions-server.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/sendMetaTransaction.ts
        ///     Payload JSON built via string.Create — single allocation, no StringBuilder.
        /// </summary>
        private async UniTask<string> PostToTransactionsServerAsync(
            string from,
            string contractAddress,
            string txData)
        {
            // Build JSON via string.Create — one allocation for the final string.
            // from, contractAddress, txData are hex strings — no JSON escaping needed.
            // Format: {"transactionData":{"from":"<from>","params":["<contractAddress>","<txData>"]}}
            int payloadLen = POST_PAYLOAD_FIXED_OVERHEAD + from.Length + contractAddress.Length + txData.Length;

            string payloadJson = string.Create(payloadLen, (from, contractAddress, txData), static (span, state) =>
            {
                int pos = 0;

                "{\"transactionData\":{\"from\":\"".AsSpan().CopyTo(span);
                pos += 28;

                state.from.AsSpan().CopyTo(span[pos..]);
                pos += state.from.Length;

                "\",\"params\":[\"".AsSpan().CopyTo(span[pos..]);
                pos += 13;

                state.contractAddress.AsSpan().CopyTo(span[pos..]);
                pos += state.contractAddress.Length;

                "\",\"".AsSpan().CopyTo(span[pos..]);
                pos += 3;

                state.txData.AsSpan().CopyTo(span[pos..]);
                pos += state.txData.Length;

                "\"]}}".AsSpan().CopyTo(span[pos..]);
            });

            IThirdwebHttpClient httpClient = client.HttpClient;

            var content = new System.Net.Http.StringContent(
                payloadJson,
                Encoding.UTF8,
                "application/json"
            );

            ThirdwebHttpResponseMessage response = await httpClient.PostAsync(
                metaTxServerUrl.Value,
                content,
                CancellationToken.None
            );

            string responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Web3Exception($"Meta-transaction relay failed: {response.StatusCode} - {responseJson}");

            TransactionsServerResponse? result = JsonConvert.DeserializeObject<TransactionsServerResponse>(responseJson);

            if (result == null || string.IsNullOrEmpty(result.txHash))
                throw new Web3Exception($"Meta-transaction relay returned empty txHash: {responseJson}");

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb Meta-transaction successful! TxHash: {result.txHash}");
            return result.txHash;
        }

        /// <summary>
        ///     Encodes the executeMetaTransaction(address,bytes,bytes32,bytes32,uint8) call.
        ///     This is what gets sent to the transactions-server as the second param.
        ///     Based on: https://github.com/decentraland/decentraland-transactions/blob/master/src/utils.ts
        /// </summary>
        private string EncodeExecuteMetaTransaction(string userAddress, string signature, string functionSignature)
        {
            // Pre-parse v value (need it for the state and to normalize 0/1 → 27/28)
            ReadOnlySpan<char> sigSpan = signature.AsSpan();

            if (sigSpan.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                sigSpan = sigSpan[2..];

            int vInt = int.Parse(sigSpan.Slice(128, 2), NumberStyles.HexNumber);

            // Normalize v value (some wallets return 0/1 instead of 27/28)
            if (vInt < 27)
                vInt += 27;

            // Determine method length (without 0x prefix) for output sizing
            int methodHexLen = functionSignature.Length;

            if (functionSignature.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                methodHexLen -= 2;

            // Integer ceiling division — avoids int→double conversion + Math.Ceiling
            int signaturePadding = (methodHexLen + ABI_WORD_HEX - 1) / ABI_WORD_HEX;

            // Total: "0x"(2) + selector(8) + 6×64 (address,offset,r,s,v,sigLen) + 64×padding (method)
            int totalLength = 2 + 8 + (6 * ABI_WORD_HEX) + (ABI_WORD_HEX * signaturePadding);

            return string.Create(
                totalLength,
                (userAddress, signature, functionSignature, vInt, signaturePadding),
                static (span, state) =>
                {
                    span.Fill('0');
                    int pos = 0;

                    // "0x" prefix
                    span[pos] = '0';
                    span[pos + 1] = 'x';
                    pos += 2;

                    // executeMetaTransaction selector = 0x0c53c51c
                    "0c53c51c".AsSpan().CopyTo(span[pos..]);
                    pos += 8;

                    // userAddress padded to 64 — NO toLowerCase, just like JS!
                    ReadOnlySpan<char> addr = state.userAddress.AsSpan();

                    if (addr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        addr = addr[2..];

                    addr.CopyTo(span.Slice(pos + ABI_WORD_HEX - addr.Length, addr.Length));
                    pos += ABI_WORD_HEX;

                    // offset to functionSignature (160 = 0xa0)
                    span[pos + 62] = 'a';
                    // span[pos + 63] is already '0' from Fill
                    pos += ABI_WORD_HEX;

                    // r and s from signature
                    ReadOnlySpan<char> sig = state.signature.AsSpan();

                    if (sig.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        sig = sig[2..];

                    sig[..64].CopyTo(span[pos..]);
                    pos += ABI_WORD_HEX;

                    sig.Slice(64, 64).CopyTo(span[pos..]);
                    pos += ABI_WORD_HEX;

                    // v — hex right-aligned in 64-char field (already zero-filled)
                    WriteHexRight(span.Slice(pos, ABI_WORD_HEX), state.vInt);
                    pos += ABI_WORD_HEX;

                    // length of functionSignature in bytes
                    ReadOnlySpan<char> method = state.functionSignature.AsSpan();

                    if (method.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        method = method[2..];

                    WriteHexRight(span.Slice(pos, ABI_WORD_HEX), method.Length / 2);
                    pos += ABI_WORD_HEX;

                    // functionSignature data (rest is '0'-padded from Fill)
                    method.CopyTo(span[pos..]);
                });
        }

        /// <summary>
        ///     Creates EIP-712 typed data JSON for meta-transaction signing.
        ///     NOTE: JS library (decentraland-transactions) does NOT lowercase addresses.
        ///     It uses account/contractAddress as-is from the wallet (usually checksum format).
        ///     EIP-712 'address' type is encoded as bytes, so case shouldn't matter for the hash.
        ///     IMPORTANT: We use explicit JSON construction to ensure exact key ordering
        ///     matches the JS library. JsonConvert with anonymous types may reorder keys.
        /// </summary>
        private string CreateMetaTxTypedData(
            string contractName,
            string contractVersion,
            string contractAddress,
            int chainIdValue,
            BigInteger nonce,
            string from,
            string functionSignature)
        {
            long nonceValue = (long)nonce;
            int contractNameJsonLen = JsonEscapedLength(contractName);
            int contractVersionJsonLen = JsonEscapedLength(contractVersion);
            int nonceDigits = CountDecimalDigits(nonceValue);

            int totalLen = TYPED_DATA_TYPES_JSON.Length + TYPED_DATA_FIXED_OVERHEAD + ABI_WORD_HEX
                           + contractNameJsonLen + contractVersionJsonLen
                           + contractAddress.Length + nonceDigits
                           + from.Length + functionSignature.Length;

            string result = string.Create(
                totalLen,
                (contractName, contractVersion, contractAddress, chainIdValue, nonceValue, from, functionSignature),
                static (span, state) =>
                {
                    int pos = 0;

                    // Types section (compile-time constant)
                    TYPED_DATA_TYPES_JSON.AsSpan().CopyTo(span);
                    pos += TYPED_DATA_TYPES_JSON.Length;

                    // "domain":{"name":
                    "\"domain\":{\"name\":".AsSpan().CopyTo(span[pos..]);
                    pos += 17;

                    pos += WriteJsonString(span[pos..], state.contractName);

                    // ,"version":
                    ",\"version\":".AsSpan().CopyTo(span[pos..]);
                    pos += 11;

                    pos += WriteJsonString(span[pos..], state.contractVersion);

                    // ,"verifyingContract":"
                    ",\"verifyingContract\":\"".AsSpan().CopyTo(span[pos..]);
                    pos += 22;

                    state.contractAddress.AsSpan().CopyTo(span[pos..]);
                    pos += state.contractAddress.Length;

                    // ","salt":"0x
                    "\",\"salt\":\"0x".AsSpan().CopyTo(span[pos..]);
                    pos += 12;

                    // Salt hex: chainId padded to bytes32
                    Span<char> saltDest = span.Slice(pos, ABI_WORD_HEX);
                    saltDest.Fill('0');
                    WriteHexRight(saltDest, state.chainIdValue);
                    pos += ABI_WORD_HEX;

                    // "},
                    "\"},".AsSpan().CopyTo(span[pos..]);
                    pos += 3;

                    // "primaryType":"MetaTransaction",
                    "\"primaryType\":\"MetaTransaction\",".AsSpan().CopyTo(span[pos..]);
                    pos += 32;

                    // "message":{"nonce":
                    "\"message\":{\"nonce\":".AsSpan().CopyTo(span[pos..]);
                    pos += 19;

                    // Nonce as decimal number — TryFormat writes directly into span
                    state.nonceValue.TryFormat(span[pos..], out int nonceWritten);
                    pos += nonceWritten;

                    // ,"from":"
                    ",\"from\":\"".AsSpan().CopyTo(span[pos..]);
                    pos += 9;

                    state.from.AsSpan().CopyTo(span[pos..]);
                    pos += state.from.Length;

                    // ","functionSignature":"
                    "\",\"functionSignature\":\"".AsSpan().CopyTo(span[pos..]);
                    pos += 23;

                    state.functionSignature.AsSpan().CopyTo(span[pos..]);
                    pos += state.functionSignature.Length;

                    // "}}
                    "\"}}".AsSpan().CopyTo(span[pos..]);
                });

            ReportHub.Log(ReportCategory.AUTHENTICATION, $"ThirdWeb Created typed data JSON (length={result.Length}):\n{result}");
            return result;
        }

        /// <summary>
        ///     Writes an integer value as lowercase hex digits right-aligned within the destination span.
        ///     The destination span must be pre-filled with '0' characters.
        /// </summary>
        private static void WriteHexRight(Span<char> dest, int value)
        {
            int pos = dest.Length - 1;

            while (value > 0 && pos >= 0)
            {
                dest[pos--] = HEX_CHARS[value & 0xF];
                value >>= 4;
            }
        }

        /// <summary>
        ///     Writes a JSON-escaped string (with surrounding double quotes) directly into the span.
        ///     Returns the number of characters written.
        /// </summary>
        private static int WriteJsonString(Span<char> dest, ReadOnlySpan<char> value)
        {
            int pos = 0;
            dest[pos++] = '"';

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        dest[pos++] = '\\';
                        dest[pos++] = '"';
                        break;
                    case '\\':
                        dest[pos++] = '\\';
                        dest[pos++] = '\\';
                        break;
                    case '\n':
                        dest[pos++] = '\\';
                        dest[pos++] = 'n';
                        break;
                    case '\r':
                        dest[pos++] = '\\';
                        dest[pos++] = 'r';
                        break;
                    case '\t':
                        dest[pos++] = '\\';
                        dest[pos++] = 't';
                        break;
                    default:
                        if (c < 0x20)
                        {
                            dest[pos++] = '\\';
                            dest[pos++] = 'u';
                            dest[pos++] = '0';
                            dest[pos++] = '0';
                            dest[pos++] = HEX_CHARS[c >> 4];
                            dest[pos++] = HEX_CHARS[c & 0xF];
                        }
                        else
                        {
                            dest[pos++] = c;
                        }

                        break;
                }
            }

            dest[pos++] = '"';
            return pos;
        }

        /// <summary>
        ///     Calculates the total length of a JSON-escaped string including surrounding double quotes.
        /// </summary>
        private static int JsonEscapedLength(string value)
        {
            int len = 2; // surrounding quotes

            foreach (char c in value)
            {
                if (c == '"' || c == '\\' || c == '\n' || c == '\r' || c == '\t')
                    len += 2;
                else if (c < 0x20)
                    len += 6; // \u00XX
                else
                    len += 1;
            }

            return len;
        }

        /// <summary>
        ///     Counts the number of decimal digits required to represent the value.
        ///     Handles zero (returns 1) and negative values (includes sign).
        /// </summary>
        private static int CountDecimalDigits(long value)
        {
            if (value == 0) return 1;

            int digits = 0;

            if (value < 0)
            {
                digits = 1; // minus sign
                value = -value;
            }

            while (value > 0)
            {
                digits++;
                value /= 10;
            }

            return digits;
        }

        /// <summary>
        ///     Contract metadata for EIP-712 domain.
        ///     ChainId is the chain where the contract is deployed (used for EIP-712 salt and RPC queries).
        /// </summary>
        private record ContractMetaTxInfo(string Name, string Version, int ChainId = 137);

        private class TransactionsServerResponse
        {
            public string? txHash { get; set; }
            public string? error { get; set; }
        }
    }
}
