using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public interface IAdapterAddresses
    {
        string AdapterUrlAsync(string commsAdapter);

        public static IAdapterAddresses NewDefault()
        {
            return new LogAdapterAddresses(
                new RefinedAdapterAddresses(),
                ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log
            );
        }
    }
}
