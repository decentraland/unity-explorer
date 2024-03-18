using CommunicationData.URLHelpers;
using System;

namespace DCL.Utilities
{
    public class NftUtils
    {
        public static bool TryParseUrn(URN urn, out string contractAddress, out string tokenId)
        {
            const char SEPARATOR = ':';
            const string DCL_URN_ID = "urn:decentraland";
            const string CHAIN_ETHEREUM = "ethereum";

            contractAddress = string.Empty;
            tokenId = string.Empty;

            try
            {
                var urnSpan = urn.ToString().AsSpan();

                // 1: "urn:decentraland"
                if (!urnSpan.Slice(0, DCL_URN_ID.Length).Equals(DCL_URN_ID, StringComparison.Ordinal))
                    return false;
                urnSpan = urnSpan.Slice(DCL_URN_ID.Length + 1);

                // TODO: allow 'matic' chain when Opensea implements its APIv2 "retrieve assets" endpoint
                // (https://docs.opensea.io/v2.0/reference/api-overview) in the future
                // 2: chain/network
                var chainSpan = urnSpan.Slice(0, CHAIN_ETHEREUM.Length);
                if (!chainSpan.Equals(CHAIN_ETHEREUM, StringComparison.Ordinal))
                    return false;
                urnSpan = urnSpan.Slice(chainSpan.Length + 1);

                // 3: contract standard
                var contractStandardSpan = urnSpan.Slice(0, urnSpan.IndexOf(SEPARATOR));
                urnSpan = urnSpan.Slice(contractStandardSpan.Length + 1);

                // 4: contract address
                var contractAddressSpan = urnSpan.Slice(0, urnSpan.IndexOf(SEPARATOR));
                urnSpan = urnSpan.Slice(contractAddressSpan.Length + 1);

                // 5: token id
                var tokenIdSpan = urnSpan;
                contractAddress = contractAddressSpan.ToString();
                tokenId = tokenIdSpan.ToString();

                return true;
            }
            catch (Exception)
            { // ignored
            }

            return false;
        }
    }
}
