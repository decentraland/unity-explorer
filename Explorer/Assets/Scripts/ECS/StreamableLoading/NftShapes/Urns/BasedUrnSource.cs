using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System;

namespace ECS.StreamableLoading.NftShapes.Urns
{
    public class BasedUrnSource : IUrnSource
    {
        private readonly string baseUrl;

        public BasedUrnSource(string baseUrl = "https://opensea.decentraland.org/api/v2/chain/ethereum/contract/{address}/nfts/{id}")
        {
            this.baseUrl = baseUrl;
        }

        public URLAddress UrlOrEmpty(string urn)
        {
            ReadOnlySpan<char> CutBeforeColon(ref int endIndex, out bool success)
            {
                int atBeginning = endIndex;

                for (; endIndex >= 0; endIndex--)
                    if (urn[endIndex] is ':')
                    {
                        success = true;
                        return urn.AsSpan().Slice(endIndex + 1, atBeginning - endIndex);
                    }

                success = false;
                return new ReadOnlySpan<char>();
            }

            void LogError()
            {
                ReportHub.LogError(ReportCategory.NFT_SHAPE_WEB_REQUEST, $"Error parsing urn: {urn}");
            }

            int index = urn.Length - 1;
            bool success;

            ReadOnlySpan<char> id = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            index--;
            ReadOnlySpan<char> address = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            return URLAddress.FromString(
                baseUrl
                   .Replace("{address}", new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                   .Replace("{id}", new string(id))
            );
        }
    }
}
