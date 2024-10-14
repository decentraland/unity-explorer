using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace CommunicationData.URLHelpers
{
    public struct URN
    {
        private const int SHORTEN_URN_PARTS = 6;
        private const int THIRD_PARTY_V2_SHORTEN_URN_PARTS = 7;
        private const string THIRD_PARTY_PART_ID = "collections-thirdparty";

        private readonly string originalUrn;
        private readonly Memory<char> lowercaseMemory;
        private readonly int cachedHashCode;

        private int cachedShortenIndex;
        private string cachedAsString;
        private URLAddress cachedUrlAddress;
        private bool hasCachedUrlAddress;

        public URN(string urn)
        {
            originalUrn = urn;
            lowercaseMemory = ComputeLowercaseMemory(originalUrn, originalUrn.Length);
            cachedHashCode = originalUrn != null ? ComputeHashCode(lowercaseMemory.Span) : 0;

            cachedShortenIndex = -2;
            cachedAsString = string.Empty;
            cachedUrlAddress = URLAddress.EMPTY;
            hasCachedUrlAddress = false;
        }

        private URN(URN urn, int shortenIndex)
        {
            originalUrn = urn.originalUrn;
            lowercaseMemory = urn.lowercaseMemory[..shortenIndex];
            cachedHashCode = originalUrn != null ? ComputeHashCode(lowercaseMemory.Span) : 0;

            cachedShortenIndex = -2;
            cachedAsString = string.Empty;
            cachedUrlAddress = URLAddress.EMPTY;
            hasCachedUrlAddress = false;
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
            lowercaseMemory.Length == 0;

        public bool IsValid() =>
            !IsNullOrEmpty() && originalUrn.StartsWith("urn");

        public bool Equals(URN other) =>
            this.cachedHashCode == other.cachedHashCode && // "fail fast" check
            lowercaseMemory.Span.SequenceEqual(other.lowercaseMemory.Span); // actual check, to avoid hashes false positives

        public override bool Equals(object? obj) =>
            obj is URN other && Equals(other);

        public override string ToString() =>
            this;

        public static implicit operator URN(int urn) =>
            urn.ToString();

        public static implicit operator string(URN urn)
        {
            if (urn.originalUrn.Length == urn.lowercaseMemory.Length)
                return urn.originalUrn;

            if (string.IsNullOrEmpty(urn.cachedAsString))
                urn.cachedAsString = urn.originalUrn[..urn.lowercaseMemory.Length];

            return urn.cachedAsString;
        }

        public static implicit operator URN(string urn) =>
            new (urn);

        public static bool operator ==(URN left, URN right) =>
            left.Equals(right);

        public static bool operator !=(URN left, URN right) =>
            !(left == right);

        public bool IsThirdPartyCollection() =>
            !IsNullOrEmpty() && originalUrn.Contains(THIRD_PARTY_PART_ID);

        public URLAddress ToUrlOrEmpty(URLAddress baseUrl)
        {
            if (hasCachedUrlAddress)
                return cachedUrlAddress;

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

            hasCachedUrlAddress = true;

            int index = currentUrn.Length - 1;

            ReadOnlySpan<char> id = CutBeforeColon(ref index, out bool success);

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
            CutBeforeColon(ref index, out success);

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

            cachedUrlAddress = URLAddress.FromString(
                baseUrl.Value
                       .Replace("{chain}", new string(chain))
                       .Replace("{address}", new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                       .Replace("{id}", new string(id))
            );

            return cachedUrlAddress;
        }

        public URN Shorten()
        {
            if (cachedShortenIndex != -2)
                return cachedShortenIndex == -1 ? this : new URN(this, cachedShortenIndex);

            if (IsNullOrEmpty() || CountParts() <= SHORTEN_URN_PARTS)
            {
                cachedShortenIndex = -1;
                return this;
            }

            cachedShortenIndex = CalculateShortenIndex();
            return cachedShortenIndex == -1 ? this : new URN(this, cachedShortenIndex);
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

    public class URNCachedEqualityComparer : IEqualityComparer<URN>
    {
        public static URNCachedEqualityComparer Default { get; } = new ();

        public bool Equals(URN x, URN y) =>
            x.Equals(y);

        public int GetHashCode(URN obj) =>
            obj.GetHashCode();
    }
}
