using System;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class RefinedAdapterAddresses : IAdapterAddresses
    {
        /// <summary>
        ///     The schemes an adapter is served over. Whatever precedes the first of them is handshake pre-info
        ///     ("fixed-adapter:signed-login:"), which the rooms downstream must not carry into the fetch, so
        ///     refining means cutting the address down to where its url starts.
        /// </summary>
        private static readonly string[] ADAPTER_SCHEMES = { "wss://", "https://", "http://" };

        private readonly string replaceRefined;

        public RefinedAdapterAddresses(string replaceRefined = "archipelago:archipelago:")
        {
            this.replaceRefined = replaceRefined;
        }

        public string AdapterUrlAsync(string unrefinedAdapter)
        {
            unrefinedAdapter = unrefinedAdapter.Replace(replaceRefined, string.Empty);

            // The earliest scheme is the url's own: a later one can only sit inside its path or query, so
            // cutting to that would truncate the url and read it as the wrong protocol.
            int urlStart = -1;

            foreach (string scheme in ADAPTER_SCHEMES)
            {
                int index = unrefinedAdapter.IndexOf(scheme, StringComparison.OrdinalIgnoreCase);

                if (index >= 0 && (urlStart < 0 || index < urlStart))
                    urlStart = index;
            }

            return urlStart > 0 ? unrefinedAdapter.Substring(urlStart) : unrefinedAdapter;
        }
    }
}
