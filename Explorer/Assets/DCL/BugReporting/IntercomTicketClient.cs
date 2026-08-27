using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.Threading;
using Utility.Times;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Creates Intercom tickets through the intercom-proxy lambda, which holds the workspace
    ///     token and verifies the reporter through Decentraland Signed Fetch.
    /// </summary>
    public class IntercomTicketClient
    {
        private const string ORIGIN_HEADER = "Origin";
        private const string SIGNATURE_METADATA = "{}";

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public IntercomTicketClient(IWebRequestController webRequestController, IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        /// <returns>The id of the created ticket.</returns>
        public virtual async UniTask<Result<string>> CreateTicketAsync(IntercomTicketData ticket, CancellationToken ct)
        {
            string url = urlsSource.Url(DecentralandUrl.IntercomTickets);

            // The proxy allowlists the web client origin of its environment and rejects any other with a 403.
            string origin = urlsSource.Url(DecentralandUrl.IntercomTicketsOrigin);
            string json = IntercomTicketPayload.BuildCreateTicketJson(in ticket);
            ulong unixTimestamp = DateTime.UtcNow.UnixTimeAsMilliseconds();

            try
            {
                IntercomTicketResponse response = await webRequestController
                                                       .PostAsync(url, GenericPostArguments.CreateJson(json), ct, ReportCategory.GENERIC_WEB_REQUEST,
                                                            headersInfo: new WebRequestHeadersInfo().Add(ORIGIN_HEADER, origin).WithSign(SIGNATURE_METADATA, unixTimestamp),
                                                            signInfo: WebRequestSignInfo.NewFromRaw(SIGNATURE_METADATA, urlsSource.GetOriginalUrl(url), unixTimestamp, "post"))
                                                       .CreateFromJson<IntercomTicketResponse>(WRJsonParser.Unity);

                return Result<string>.SuccessResult(response.id);
            }
            catch (OperationCanceledException) { return Result<string>.CancelledResult(); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.GENERIC_WEB_REQUEST);
                return Result<string>.ErrorResult($"The ticket could not be created: {e.Message}");
            }
        }
    }
}
