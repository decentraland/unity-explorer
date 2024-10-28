using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        private const int SHORTEN_URN_PARTS = 6;
        private const int THIRD_PARTY_V2_SHORTEN_URN_PARTS = 7;
        private const string THIRD_PARTY_PART_ID = "collections-thirdparty";

        private readonly string lowercaseUrn;
        private readonly string originalUrn;
        private readonly int hash;

        public URN(string urn)
        {
            originalUrn = urn;
            lowercaseUrn = originalUrn.ToLower();
            hash = originalUrn != null ? lowercaseUrn.GetHashCode() : 0;
        }

        private URN(URN urn, int shortenIndex)
        {
            bool hasSameLength = shortenIndex == urn.originalUrn.Length;

            originalUrn = hasSameLength ? urn.originalUrn : urn.originalUrn[..shortenIndex];
            lowercaseUrn = hasSameLength ? urn.lowercaseUrn : urn.lowercaseUrn[..shortenIndex];

            hash = hasSameLength
                ? urn.hash
                : originalUrn != null
                    ? lowercaseUrn.GetHashCode()
                    : 0;
        }

        public bool IsNullOrEmpty() =>
            string.IsNullOrEmpty(originalUrn);

        public bool IsValid() =>
            !IsNullOrEmpty() && originalUrn.StartsWith("urn");

        public bool Equals(URN other) =>
            hash == other.hash && // "fail fast" check
            lowercaseUrn == other.lowercaseUrn; // check to avoid false positives from hash collisions

        public override bool Equals(object obj) =>
            obj is URN other && Equals(other);

        public override string ToString() =>
            originalUrn;

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

            void LogError()
            {
                ReportHub.LogError(ReportCategory.NFT_SHAPE_WEB_REQUEST, $"Error parsing urn: {currentUrn}");
            }

            int index = currentUrn.Length - 1;
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

            index--;
            ReadOnlySpan<char> ercType = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            index--;
            ReadOnlySpan<char> chain = CutBeforeColon(ref index, out success);

            if (success == false)
            {
                LogError();
                return URLAddress.EMPTY;
            }

            return URLAddress.FromString(
                baseUrl.Value
                       .Replace("{chain}", new string(chain))
                       .Replace("{address}", new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                       .Replace("{id}", new string(id))
            );
        }

        public override int GetHashCode() =>
            hash;

        public URN Shorten()
        {
            if (string.IsNullOrEmpty(originalUrn)) return this;
            if (CountParts() <= SHORTEN_URN_PARTS) return this;

            int shortenIndex = GetShortenIndex();

            return shortenIndex != -1 ? new URN(this, shortenIndex) : this;
        }

        private int GetShortenIndex()
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

                return index;
            }

            // TokenId is always placed in the last part for regular nfts
            index = originalUrn.LastIndexOf(':');

            return index;
        }

        public static implicit operator URN(int urn) =>
            urn.ToString();

        public static implicit operator string(URN urn) =>
            urn.originalUrn;

        public static implicit operator URN(string urn) =>
            new (urn);

        public bool IsThirdPartyCollection() =>
            !string.IsNullOrEmpty(originalUrn) && originalUrn.Contains(THIRD_PARTY_PART_ID);

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

    public class URNIgnoreCaseEqualityComparer : IEqualityComparer<URN>
    {
        public static URNIgnoreCaseEqualityComparer Default { get; } = new ();

        public bool Equals(URN x, URN y) =>
            x.Equals(y);

        public int GetHashCode(URN obj) =>
            obj.GetHashCode();
    }
}
