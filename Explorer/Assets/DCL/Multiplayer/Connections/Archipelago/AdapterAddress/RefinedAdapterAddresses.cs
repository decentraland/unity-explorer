using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public class RefinedAdapterAddresses : IAdapterAddresses
    {
        private readonly string replaceRefined;

        public RefinedAdapterAddresses(string replaceRefined = "archipelago:archipelago:")
        {
            this.replaceRefined = replaceRefined;
        }

        public string AdapterUrlAsync(string unrefinedAdapter)
        {
            unrefinedAdapter = unrefinedAdapter.Replace(replaceRefined, string.Empty);
            unrefinedAdapter = ReturnHttpsProtocolURL(unrefinedAdapter);
            unrefinedAdapter = ReturnWSSProtocolURL(unrefinedAdapter);
            unrefinedAdapter = AddWSSPreInfoIfProtocolMissing(unrefinedAdapter);
            return unrefinedAdapter;
        }

        private string ReturnHttpsProtocolURL(string url)
        {
            int index = url.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }

        private string ReturnWSSProtocolURL(string url)
        {
            int index = url.IndexOf("wss://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }

        private string AddWSSPreInfoIfProtocolMissing(string url)
        {
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return url;

            if (url.StartsWith("ws-room:"))
                return $"wss://{url.Substring(8)}";

            return url;
        }
    }
}
