using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;

namespace DCL.Multiplayer.Connections.Archipelago.AdapterAddress
{
    public interface IAdapterAddresses
    {
        UniTask<string> AdapterUrlAsync(string aboutUrl, CancellationToken token);

        public static IAdapterAddresses NewDefault(IWebRequestController webRequestController) =>
            new LogAdapterAddresses(
                new RefinedAdapterAddresses(
                    new WebRequestsAdapterAddresses(webRequestController)
                ),
                ReportHub.WithReport(ReportCategory.ARCHIPELAGO_REQUEST).Log
            );
    }
}
