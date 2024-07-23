using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        private const int SHORTEN_URN_PARTS = 6;
        private const string THIRD_PARTY_PART_ID = "collections-thirdparty";

        private readonly string urn;

        public URN(string urn)
        {
            this.urn = urn;
        }

        public URN(int urn)
        {
            this.urn = urn.ToString();
        }

        public bool IsNullOrEmpty() =>
            string.IsNullOrEmpty(urn);

        public bool IsValid() =>
            !IsNullOrEmpty() && urn.StartsWith("urn");

        public bool Equals(int other) => Equals(other.ToString());

        public bool Equals(URN other) =>
            Equals(other.urn);

        public bool Equals(string other) =>
            // Ignore case of all urn since the server returns urns with lower case or upper case representing the same content on different endpoints
            // For example a wearable in the profile (/lambdas/profiles/:address):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bD77619a75C8EdA181e3587339E7011DA75bF0E:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            // The same wearable in the content server (/content/entities/active):
            // urn:decentraland:matic:collections-thirdparty:dolcegabbana-disco-drip:0x4bd77619a75c8eda181e3587339e7011da75bf0e:2a424e9c-c6fb-4783-99ed-63d260d90ed2
            string.Equals(urn, other, StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj) =>
            obj is URN other && Equals(other);

        public override string ToString() =>
            urn;

        public URLAddress ToUrlOrEmpty(URLAddress baseUrl)
        {
            string currentUrn = this.urn;
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
            urn != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(urn) : 0;

        public URN Shorten()
        {
            if (string.IsNullOrEmpty(urn)) return urn;
            // Third party collections do not include the tokenId and have 7 parts, so we must keep all of them
            if (IsThirdPartyCollection()) return urn;

            int index = -1;

            for (var i = 0; i < SHORTEN_URN_PARTS; i++)
            {
                index = urn.IndexOf(':', index + 1);
                if (index == -1) break;
            }

            return index != -1 ? urn[..index] : urn;
        }

        public bool IsExtended()
        {
            // Third party collections do not apply to shortened/extended rules
            if (IsThirdPartyCollection()) return false;

            var count = 0;

            foreach (char c in urn)
                if (c == ':')
                    count++;

            return count >= SHORTEN_URN_PARTS;
        }

        public static implicit operator URN(int urn) =>
            urn.ToString();

        public static implicit operator string(URN urn) =>
            urn.urn;

        public static implicit operator URN(string urn) =>
            new (urn);

        private bool IsThirdPartyCollection() =>
            !string.IsNullOrEmpty(urn) && urn.Contains(THIRD_PARTY_PART_ID);
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
