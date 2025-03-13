using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class UserIPAddressService
    {
        private const int REQUEST_TIMEOUT = 5;
        private const int ATTEMPTS_COUNT = 1;

        private readonly string[] ipv4Services = {
            "https://checkip.amazonaws.com",
            "https://api.ipify.org",
            "https://api4.my-ip.io/ip",
            "https://ipv4.icanhazip.com",
            "https://ipinfo.io/ip",
            "https://ip4.seeip.org",
            "https://v4.ident.me/",
            "https://ipv4.myexternalip.com/raw"
        };

        private readonly IWebRequestController webRequestController;

        public string LocalIP { get; }
        private string publicIP;

        public UserIPAddressService(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;

            LocalIP = GetLocalIPAddress();
            publicIP = string.Empty;
            GetPublicIPAddressAsync().Forget();
        }

        private static string GetLocalIPAddress()
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (IPAddress? ip in host.AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    return ip.ToString();

            return string.Empty;
        }

        public async UniTask<string> GetPublicIPAddressAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(publicIP))
                return publicIP;

            foreach (string serviceUrl in ipv4Services)
            {
                try
                {
                    var commonArguments = new CommonArguments(
                        URLAddress.FromString(serviceUrl),
                        attemptsCount: ATTEMPTS_COUNT,
                        timeout: REQUEST_TIMEOUT
                    );

                    publicIP = await webRequestController
                                   .GetAsync(commonArguments, cancellationToken, ReportCategory.GENERIC_WEB_REQUEST)
                                   .StoreTextAsync();

                    publicIP = publicIP.Trim('\r', '\n', ' ', '\t');

                    if (!string.IsNullOrWhiteSpace(publicIP) && IPAddress.TryParse(publicIP, out _))
                        return publicIP;

                    publicIP = string.Empty;
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                        return string.Empty;

                    ReportHub.LogWarning(ReportCategory.ANALYTICS, $"Failed to get IP from {serviceUrl}. Error: {ex.Message}");
                }
            }

            ReportHub.LogWarning(ReportCategory.ANALYTICS, "Failed to get public IP address from any service");
            return string.Empty;
        }
    }
}
