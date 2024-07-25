using CommunicationData.URLHelpers;
using System;

namespace DCL.Utilities
{
    public class NftUtils
    {
        public static bool TryParseUrn(URN urn, out string chain, out string contractAddress, out string tokenId)
        {
            const char SEPARATOR = ':';
            const string DCL_URN_ID = "urn:decentraland";
            const string COLLECTIONS_THIRDPARTY = "collections-thirdparty";

            contractAddress = string.Empty;
            tokenId = string.Empty;
            chain = string.Empty;

            try
            {
                var urnSpan = urn.ToString().AsSpan();

                // 1: "urn:decentraland"
                if (!urnSpan.Slice(0, DCL_URN_ID.Length).Equals(DCL_URN_ID, StringComparison.Ordinal))
                    return false;
                urnSpan = urnSpan.Slice(DCL_URN_ID.Length + 1);

                // 2: chain/network
                var chainSpan = urnSpan.Slice(0, urnSpan.IndexOf(SEPARATOR));
                urnSpan = urnSpan.Slice(chainSpan.Length + 1);

                // 3: contract standard
                var contractStandardSpan = urnSpan.Slice(0, urnSpan.IndexOf(SEPARATOR));
                urnSpan = urnSpan.Slice(contractStandardSpan.Length + 1);

                // check if wearables is third-party
                if (contractStandardSpan.ToString().Equals(COLLECTIONS_THIRDPARTY, StringComparison.Ordinal))
                {
                    // 4: contract address
                    var contractAddressSpan = urnSpan;
                    contractAddress = contractAddressSpan.ToString();

                    // NOTE: Third Party wearables do not have token id at the moment
                }
                else
                {
                    // 4: contract address
                    var contractAddressSpan = urnSpan.Slice(0, urnSpan.IndexOf(SEPARATOR));
                    urnSpan = urnSpan.Slice(contractAddressSpan.Length + 1);

                    // 5: token id
                    var tokenIdSpan = urnSpan;
                    contractAddress = contractAddressSpan.ToString();
                    tokenId = tokenIdSpan.ToString();
                }

                chain = chainSpan.ToString();
                return true;
            }
            catch (Exception)
            { // ignored
            }

            return false;
        }
    }
}
