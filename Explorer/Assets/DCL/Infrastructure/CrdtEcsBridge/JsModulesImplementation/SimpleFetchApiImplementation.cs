using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Microsoft.ClearScript;
using SceneRuntime.Apis.Modules.FetchApi;
using SceneRuntime.ScenePermissions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using static CrdtEcsBridge.JsModulesImplementation.SimpleFetchAdHoc;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class SimpleFetchApiImplementation : ISimpleFetchApi
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum RequestMethod
        {
            GET,
            POST,
            PUT,
            DELETE,
            PATCH,
            HEAD,
            INVALID,
        }

        private readonly SceneShortInfo sceneShortInfo;
        private readonly IJsApiPermissionsProvider permissionsProvider;
        private readonly IProfileRepository profileRepository;

        public SimpleFetchApiImplementation(SceneShortInfo sceneShortInfo, IJsApiPermissionsProvider permissionsProvider, IProfileRepository profileRepository)
        {
            this.sceneShortInfo = sceneShortInfo;
            this.permissionsProvider = permissionsProvider;
            this.profileRepository = profileRepository;
        }

        public void Dispose() { }

        public async UniTask<ISimpleFetchApi.Response> FetchAsync(
            string requestMethod,
            string url,
            object headers,
            bool hasBody,
            string body,
            string redirect,
            int timeout,
            IWebRequestController webController,
            CancellationToken ct,
            bool isLocalSceneDevelopment
        )
        {
            if (!permissionsProvider.CanInvokeFetchAPI()) return default(ISimpleFetchApi.Response);

            try
            {
                // if we're in LocalSceneDevelopment mode to allow connecting to unsafe websocket server to the client
                if (!isLocalSceneDevelopment && !IsHttps(url))
                    throw new Exception("Can't make an unsafe http request, please upgrade to https. url=" + url);

                RequestMethod parsedRequestMethod = ParseRequestMethod(requestMethod);

                if (parsedRequestMethod == RequestMethod.INVALID)
                    throw new ArgumentException("Invalid request method.");

                var commonArguments = new CommonArguments(URLAddress.FromString(url), RetryPolicy.HEADER_REQUIRED, timeout: timeout);
                WebRequestHeadersInfo webRequestHeaders = HeadersFromJsObject(headers);

                await UniTask.SwitchToMainThread();

                ISimpleFetchApi.Response response;

                switch (parsedRequestMethod)
                {
                    case RequestMethod.GET:
                        response = await webController.GetAsync<GenerateResponseOp<GenericGetRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericGetRequest>(), ct, GetReportData(), webRequestHeaders);
                        break;
                    case RequestMethod.POST:
                        string postContentType = webRequestHeaders.HeaderContentType();
                        var postArguments = GenericPostArguments.Create(body, postContentType);
                        response = await webController.InterceptPostAsync(profileRepository, commonArguments, postArguments, ct, GetReportData(), webRequestHeaders);
                        break;
                    case RequestMethod.PUT:
                        string putContentType = webRequestHeaders.HeaderContentType();
                        var putArguments = GenericPostArguments.Create(body, putContentType);
                        response = await webController.PutAsync<GenerateResponseOp<GenericPutRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPutRequest>(), putArguments, ct, GetReportData(), webRequestHeaders);
                        break;
                    case RequestMethod.DELETE:
                        string deleteContentType = webRequestHeaders.HeaderContentType();
                        var deleteArguments = GenericPostArguments.Create(body, deleteContentType);
                        response = await webController.DeleteAsync<GenerateResponseOp<GenericDeleteRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericDeleteRequest>(), deleteArguments, ct, GetReportData(), webRequestHeaders);
                        break;
                    case RequestMethod.PATCH:
                        string patchContentType = webRequestHeaders.HeaderContentType();
                        var patchArguments = GenericPostArguments.Create(body, patchContentType);
                        response = await webController.PatchAsync<GenerateResponseOp<GenericPatchRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPatchRequest>(), patchArguments, ct, GetReportData(), webRequestHeaders);
                        break;
                    case RequestMethod.HEAD: throw new NotImplementedException();
                    case RequestMethod.INVALID:
                    default: throw new ArgumentOutOfRangeException();
                }

                // fetch() is https-only outside local scene development. A request can end on a different URL
                // scheme than requested after following redirects, so enforce the same policy on the final URL,
                // not only the initial one, and keep the response consistent with the requested scheme. Covers
                // the success path of every method; the catch below only returns error text on a failed request.
                if (ShouldBlockNonHttps(response, isLocalSceneDevelopment))
                    ReportHub.LogWarning(GetReportData(), $"Dropped fetch response: final URL is not https after redirect: {response.URL}");

                return EnforceHttpsFinalScheme(response, isLocalSceneDevelopment);
            }
            catch (UnityWebRequestException e)
            {
                return new ISimpleFetchApi.Response
                {
                    Ok = false,
                    Status = (int)e.ResponseCode,
                    StatusText = e.ResponseCode.ToString(),
                    Data = e.Text,
                    Headers = e.ResponseHeaders,
                };
            }
        }

        private ReportData GetReportData() =>
            new (ReportCategory.SCENE_FETCH_REQUEST, sceneShortInfo: sceneShortInfo);

        private static bool IsHttps(string url) =>
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     True when the final URL is no longer https (outside local scene development) and the response
        ///     should be dropped. Local scene development is exempt: it permits non-https by design (see the
        ///     initial check).
        /// </summary>
        internal static bool ShouldBlockNonHttps(ISimpleFetchApi.Response response, bool isLocalSceneDevelopment) =>
            !isLocalSceneDevelopment && !(response.URL != null && IsHttps(response.URL));

        /// <summary>
        ///     Returns the response unchanged when its final URL is still https (or in local scene development),
        ///     otherwise replaces it with an empty error response so the scene only receives https responses.
        /// </summary>
        internal static ISimpleFetchApi.Response EnforceHttpsFinalScheme(ISimpleFetchApi.Response response, bool isLocalSceneDevelopment)
        {
            if (!ShouldBlockNonHttps(response, isLocalSceneDevelopment))
                return response;

            return new ISimpleFetchApi.Response
            {
                Ok = false,
                Status = 0,
                StatusText = "Blocked non-https redirect",
                URL = response.URL,
                Data = string.Empty,
                Headers = null,
                Type = "error",
            };
        }

        private static WebRequestHeadersInfo HeadersFromJsObject(object headers)
        {
            var webRequestHeaders = new WebRequestHeadersInfo();

            if (headers is IScriptObject scriptObject)
            {
                IEnumerable<string> propertyNames = scriptObject.PropertyNames.EnsureNotNull();

                foreach (string name in propertyNames)
                {
                    var property = scriptObject.GetProperty(name).EnsureNotNull().ToString()!;
                    webRequestHeaders.Add(name, property);
                }
            }

            return webRequestHeaders;
        }

        private static RequestMethod ParseRequestMethod(string request) =>
            Enum.TryParse(request, true, out RequestMethod method) ? method : RequestMethod.INVALID;
    }
}
