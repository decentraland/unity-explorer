using DCL.Diagnostics;
using System;

namespace CommunicationData.URLHelpers
{
    public readonly struct URN
    {
        public const int SHORTEN_URN_PARTS = 6;

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
            urn == other;

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

            return URLAddress.FromString(
                baseUrl.Value
                       .Replace("{address}", new string(address)) //may be optimized further, or create custom ReplaceMethod that works with spans
                       .Replace("{id}", new string(id))
            );
        }

        public override int GetHashCode() =>
            urn != null ? urn.GetHashCode() : 0;

        public URN Shorten()
        {
            if (string.IsNullOrEmpty(urn)) return urn;

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
    }
}
