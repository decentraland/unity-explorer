using CodeLess.Interfaces;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.Profiles
{
    public partial class RealmNftNamesProvider : INftNamesProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        public RealmNftNamesProvider(IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<INftNamesProvider.PaginatedNamesResponse> GetAsync(Web3Address userId, int pageNumber, int pageSize, CancellationToken ct)
        {
            using PooledObject<URLBuilder> _ = urlsSource.BuildFromDomain(DecentralandUrl.Lambdas, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath($"users/{userId}/names"));
            urlBuilder.AppendParameter(new URLParameter("pageNum", pageNumber.ToString()));
            urlBuilder.AppendParameter(new URLParameter("pageSize", pageSize.ToString()));

            GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter = webRequestController.GetAsync(
                new CommonArguments(urlBuilder.Build()), ct, ReportCategory.REALM, ignoreErrorCodes: IWebRequestController.IGNORE_NOT_FOUND);

            RealmNamesResponse jsonResponse = await adapter.CreateFromJson<RealmNamesResponse>(WRJsonParser.Unity);

            var response = new INftNamesProvider.PaginatedNamesResponse(jsonResponse.totalAmount, jsonResponse.elements.Select(element => element.name));

            return response;
        }
    }
}
