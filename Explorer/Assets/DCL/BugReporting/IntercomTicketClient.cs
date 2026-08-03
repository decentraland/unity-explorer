using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.BugReporting
{
    /// <summary>
    ///     Creates Intercom tickets through the intercom-proxy lambda, which holds the workspace
    ///     token and verifies the reporter through Decentraland Signed Fetch.
    /// </summary>
    public class IntercomTicketClient
    {
        private const string ORIGIN_HEADER = "Origin";

        // The proxy allowlists the web client origin and rejects requests without it with a 403.
        private const string ORIGIN = "https://play.decentraland.org";
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
            string json = IntercomTicketPayload.BuildCreateTicketJson(in ticket);

            try
            {
                IntercomTicketResponse response = await webRequestController
                                                       .SignedFetchPostAsync(url, GenericPostArguments.CreateJson(json), SIGNATURE_METADATA, new WebRequestHeadersInfo().Add(ORIGIN_HEADER, ORIGIN), ct)
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
