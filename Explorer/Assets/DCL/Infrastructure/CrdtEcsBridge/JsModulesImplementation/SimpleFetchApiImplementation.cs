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

            int goldenTicket = GoldenReleaseQueue.Enabled ? GoldenReleaseQueue.TakeTicket() : 0;

            // Golden-capture determinism: live-data services compute time-relative content
            // server-side per request (event countdowns and orderings), so their responses can
            // never match across boots. Scenes get a fixed empty payload instead and render
            // their deterministic empty state.
            if (System.Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") == "1")
            {
                if (url.Contains("events.decentraland", StringComparison.OrdinalIgnoreCase) || url.Contains("events.dcl", StringComparison.OrdinalIgnoreCase))
                {
                    await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);
                    return FixedJson(url, "{\"ok\":true,\"data\":[],\"total\":0}");
                }

                // The photo mural rotates live camera-reel uploads; the feed changes between any
                // two boots, so every run renders the mural's deterministic empty state instead.
                if (url.Contains("camera-reel", StringComparison.OrdinalIgnoreCase))
                {
                    await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);
                    return FixedJson(url, "{\"images\":[],\"currentImages\":0,\"maxImages\":0}");
                }

                // The plaza pulls schedule/config from Google Sheets, which rate-limits or hiccups
                // per boot; scenes branch visibly on success vs failure (the theater marquee text),
                // so every run takes the failure branch it already renders deterministically.
                if (url.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase) || url.Contains("sheets.googleapis", StringComparison.OrdinalIgnoreCase))
                {
                    await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);

                    return new ISimpleFetchApi.Response
                    {
                        Ok = false,
                        Status = 500,
                        StatusText = "Internal Server Error",
                        URL = url,
                        Type = "basic",
                        Data = "{}",
                        Headers = new Dictionary<string, string> { ["content-type"] = "application/json" },
                    };
                }
            }

            try
            {
                // if we're in LocalSceneDevelopment mode to allow connecting to unsafe websocket server to the client
                if (!isLocalSceneDevelopment && !url.ToLower().StartsWith("https://"))
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

                if (goldenTicket > 0) await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);
                return response;
            }
            catch (UnityWebRequestException e)
            {
                if (goldenTicket > 0) await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);

                return new ISimpleFetchApi.Response
                {
                    Ok = false,
                    Status = (int)e.ResponseCode,
                    StatusText = e.ResponseCode.ToString(),
                    Data = e.Text,
                    Headers = e.ResponseHeaders,
                };
            }
            catch (Exception) when (goldenTicket > 0)
            {
                // Every taken ticket must release or the queue starves behind it.
                await GoldenReleaseQueue.AwaitTurnAsync(goldenTicket, ct);
                throw;
            }
        }

        private static ISimpleFetchApi.Response FixedJson(string url, string payload) =>
            new ()
            {
                Ok = true,
                Status = 200,
                StatusText = "OK",
                URL = url,
                Type = "basic",
                Data = payload,
                Headers = new Dictionary<string, string> { ["content-type"] = "application/json" },
            };

        private ReportData GetReportData() =>
            new (ReportCategory.SCENE_FETCH_REQUEST, sceneShortInfo: sceneShortInfo);

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
