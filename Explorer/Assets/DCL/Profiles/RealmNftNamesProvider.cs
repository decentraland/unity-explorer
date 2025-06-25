using CodeLess.Interfaces;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3;
using DCL.WebRequests;
using ECS;
using System.Linq;
using System.Threading;

namespace DCL.Profiles
{
    public partial class RealmNftNamesProvider : INftNamesProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IRealmData realm;
        private readonly URLBuilder urlBuilder = new ();

        public RealmNftNamesProvider(IWebRequestController webRequestController,
            IRealmData realm)
        {
            this.webRequestController = webRequestController;
            this.realm = realm;
        }

        public async UniTask<INftNamesProvider.PaginatedNamesResponse> GetAsync(Web3Address userId, int pageNumber, int pageSize, CancellationToken ct)
        {
            urlBuilder.Clear();
            urlBuilder.AppendDomain(realm.Ipfs.LambdasBaseUrl);
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
