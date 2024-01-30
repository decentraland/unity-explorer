using DCL.Diagnostics;
using System;

namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        private readonly string urn;

        public URN(string urn)
        {
            this.urn = urn;
        }

        public bool Equals(URN other) =>
            Equals(other.urn);

        public bool Equals(string other) =>
            urn == other;

        public override bool Equals(object obj) =>
            obj is URN other && Equals(other);

        public override string ToString() =>
            urn;

        public URLAddress ToUrlOrEmpty()
        {
            return ToUrlOrEmpty(
                URLAddress.FromString(
                    "https://opensea.decentraland.org/api/v2/chain/ethereum/contract/{address}/nfts/{id}"
                )
            );
        }

        public URLAddress ToUrlOrEmpty(URLAddress baseUrl)
        {
            var currentUrn = urn;

            ReadOnlySpan<char> CutBeforeColon(ref int endIndex, out bool success)
            {
                var atBeginning = endIndex;

                for (; endIndex >= 0; endIndex--)
                    if (currentUrn[endIndex] is ':')
                    {
                        success = true;
                        return currentUrn.AsSpan().Slice(endIndex + 1, atBeginning - endIndex);
                    }

                success = false;
                return new ReadOnlySpan<char>();
            }

            void LogError()
            {
                ReportHub.LogError(ReportCategory.NFT_SHAPE_WEB_REQUEST, $"Error parsing urn: {currentUrn}");
            }

            var index = currentUrn.Length - 1;
            bool success;

            var id = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            index--;
            var address = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            return URLAddress.FromString(
                baseUrl.Value
                    .Replace("{address}",
                        new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                    .Replace("{id}", new string(id))
            );
        }

        public override int GetHashCode() =>
            urn != null ? urn.GetHashCode() : 0;

        public string Shorten(int parts)
        {
            int index = -1;

            for (var i = 0; i < parts; i++)
            {
                index = urn.IndexOf(':', index + 1);
                if (index == -1) break;
            }

            return index != -1 ? urn[..index] : urn;
        }

        public static implicit operator string(URN urn) =>
            urn.urn;

        public static implicit operator URN(string urn) =>
            new (urn);
    }
}
