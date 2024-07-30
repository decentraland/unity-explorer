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
            unrefinedAdapter = RemoveHttpsPreInfo(unrefinedAdapter);
            unrefinedAdapter = RemoveWssPreInfo(unrefinedAdapter);
            return unrefinedAdapter;
        }

        private string RemoveHttpsPreInfo(string url)
        {
            int index = url.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }

        private string RemoveWssPreInfo(string url)
        {
            int index = url.IndexOf("wss://", StringComparison.OrdinalIgnoreCase);
            return index == -1 ? url : url.Substring(index);
        }
    }
}
