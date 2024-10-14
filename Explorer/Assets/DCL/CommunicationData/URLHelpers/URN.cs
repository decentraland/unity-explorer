using DCL.Diagnostics;
using System;

namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        private const int SHORTEN_URN_PARTS = 6;
        private const int THIRD_PARTY_V2_SHORTEN_URN_PARTS = 7;
        private const string THIRD_PARTY_PART_ID = "collections-thirdparty";

        private readonly string originalUrn;
        private readonly Memory<char> lowercaseMemory;
        private readonly int cachedHashCode;

        public URN(string urn)
        {
            originalUrn = urn;
            lowercaseMemory = ComputeLowercaseMemory(originalUrn, originalUrn.Length);
            this.cachedHashCode = originalUrn != null ? ComputeHashCode(this.lowercaseMemory.Span) : 0;
        }

        private URN(URN urn, int shortenIndex)
        {
            this.originalUrn = urn.originalUrn;
            this.lowercaseMemory = urn.lowercaseMemory[..shortenIndex];
            this.cachedHashCode = originalUrn != null ? ComputeHashCode(this.lowercaseMemory.Span) : 0;
        }

        private static Memory<char> ComputeLowercaseMemory(string input, int length)
        {
            var lowercaseChars = new char[length];
            for (var i = 0; i < length; i++)
                lowercaseChars[i] = char.ToLowerInvariant(input[i]);
            return new Memory<char>(lowercaseChars);
        }

        private static int ComputeHashCode(ReadOnlySpan<char> span)
        {
            var hash = 17;

            for (var i = 0; i < span.Length; i++)
                hash = (hash * 31) + span[i];

            return hash;
        }

        public override int GetHashCode() =>
            cachedHashCode;

        public bool IsNullOrEmpty() =>
            string.IsNullOrEmpty(originalUrn);

        public bool IsValid() =>
            !IsNullOrEmpty() && originalUrn.StartsWith("urn");

        public bool Equals(URN other) =>
            lowercaseMemory.Span.SequenceEqual(other.lowercaseMemory.Span);

        public override bool Equals(object? obj) =>
            obj is URN other && Equals(other);

        public override string ToString() => originalUrn.Length == lowercaseMemory.Length ?
            originalUrn : originalUrn[..lowercaseMemory.Length];

        public static implicit operator URN(int urn) =>
            urn.ToString();

        public static implicit operator string(URN urn) => urn.originalUrn.Length == urn.lowercaseMemory.Length ?
            urn.originalUrn : urn.originalUrn[..urn.lowercaseMemory.Length];

        public static implicit operator URN(string urn) =>
            new (urn);

        public static bool operator ==(URN left, URN right) => left.Equals(right);

        public static bool operator !=(URN left, URN right) => !(left == right);

        public bool IsThirdPartyCollection() =>
            !string.IsNullOrEmpty(originalUrn) && originalUrn.Contains(THIRD_PARTY_PART_ID);

        public URLAddress ToUrlOrEmpty(URLAddress baseUrl)
        {
            string currentUrn = originalUrn;

            ReadOnlySpan<char> CutBeforeColon(ref int endIndex, out bool success)
            {
                int atBeginning = endIndex;

                for (; endIndex >= 0; endIndex--)
                    if (currentUrn[endIndex] is ':')
                    {
                        success = true;
                        return currentUrn.AsSpan().Slice(endIndex + 1, atBeginning - endIndex);
                    }

                success = false;
                return new ReadOnlySpan<char>();
            }

            void LogError() { ReportHub.LogError(ReportCategory.NFT_SHAPE_WEB_REQUEST, $"Error parsing urn: {currentUrn}"); }

            int index = currentUrn.Length - 1;

            ReadOnlySpan<char> id = CutBeforeColon(ref index, out bool success);
            if (success == false) { LogError(); return URLAddress.EMPTY; }

            index--;
            ReadOnlySpan<char> address = CutBeforeColon(ref index, out success);
            if (success == false) { LogError(); return URLAddress.EMPTY; }

            index--;
            CutBeforeColon(ref index, out success);
            if (success == false) { LogError(); return URLAddress.EMPTY; }

            index--;
            ReadOnlySpan<char> chain = CutBeforeColon(ref index, out success);
            if (success == false) { LogError(); return URLAddress.EMPTY; }

            return URLAddress.FromString(
                baseUrl.Value
                       .Replace("{chain}", new string(chain))
                       .Replace("{address}", new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                       .Replace("{id}", new string(id))
            );
        }

        public URN Shorten()
        {
            if (string.IsNullOrEmpty(originalUrn)) return this;
            if (CountParts() <= SHORTEN_URN_PARTS) return this;

            int shortenIndex = CalculateShortenIndex();
            return shortenIndex == -1 ? this : new URN(this, shortenIndex);
        }

        private int CalculateShortenIndex()
        {
            int index;
            if (IsThirdPartyCollection())
            {
                index = -1;

                // Third party v2 contains 10 parts, on which 3 are reserved for the tokenId
                // "id": urn:decentraland:amoy:collections-thirdparty:back-to-the-future:amoy-eb54:tuxedo-6751:amoy:0x1d9fb685c257e74f869ba302e260c0b68f5ebb37:12
                // "tokenId": amoy:0x1d9fb685c257e74f869ba302e260c0b68f5ebb37:12
                for (var i = 0; i < THIRD_PARTY_V2_SHORTEN_URN_PARTS; i++)
                {
                    index = originalUrn.IndexOf(':', index + 1);
                    if (index == -1) break;
                }
            }
            else
                index = originalUrn.LastIndexOf(':'); // TokenId is always placed in the last part for regular nfts

            return index;
        }

        private int CountParts()
        {
            var count = 1;
            int index = originalUrn.IndexOf(':');

            while (index != -1)
            {
                count++;
                index = originalUrn.IndexOf(':', index + 1);
            }

            return count;
        }
    }
}
