using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System;
using System.Linq;

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

        public URLAddress UrlOrEmptyAlloc(string urn)
        {
            //"https://opensea.decentraland.org/api/v2/chain/ethereum/contract/0x06012c8cf97bead5deae237070f9587f8e7a266d/nfts/1631847"
            //urn:decentraland:ethereum:erc721:0x06012c8cf97bead5deae237070f9587f8e7a266d:1631847

            try
            {
                string[] array = urn.Split(':')!.TakeLast(2).ToArray();
                string address = array[0]!;
                string id = array[1]!;

                string completeUrl = baseUrl
                                    .Replace("{address}", address)
                                    .Replace("{id}", id);

                return URLAddress.FromString(completeUrl);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.NFT_SHAPE_WEB_REQUEST, $"Error parsing urn: {e}");
                return URLAddress.EMPTY;
            }
        }
    }
}
