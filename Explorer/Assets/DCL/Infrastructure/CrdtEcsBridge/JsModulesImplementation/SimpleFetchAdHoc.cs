using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using Newtonsoft.Json;
using SceneRuntime.Apis.Modules.FetchApi;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.CrdtEcsBridge.JsModulesImplementation
{
    public static class SimpleFetchAdHoc
    {
        private const string PROFILES_SUFFIX = "lambdas/profiles";
        private static readonly JsonSerializerSettings PROFILE_SERIALIZER_SETTINGS = new (RealmProfileRepository.SERIALIZER_SETTINGS);

        private static readonly Dictionary<string, string> DEFAULT_PROFILES_HEADERS = new ()
        {
            ["content-type"] = "application/json",
            [":status"] = "200",
        };

        /// <summary>
        ///     Redirect a profiles request to a common scheme instead of performing a plain direct request
        /// </summary>
        public static async UniTask<ISimpleFetchApi.Response> InterceptPostAsync(this IWebRequestController webRequestController,
            IProfileRepository profileRepository, CommonArguments commonArguments, GenericPostArguments args, CancellationToken ct, ReportData reportData,
            WebRequestHeadersInfo webRequestHeader)
        {
            if (IsProfilesEndpoint(commonArguments.URL))
            {
                ProfilesPostRequest request = JsonConvert.DeserializeObject<ProfilesPostRequest>(args.PostData);

                // if ids == null it's not the desired request
                if (request.ids != null)
                {
                    List<Profile>? profiles = await profileRepository.GetAsync(request.ids, ct);
                    string body = JsonConvert.SerializeObject(profiles, Formatting.None, PROFILE_SERIALIZER_SETTINGS);

                    var result = new ISimpleFetchApi.Response
                    {
                        Headers = DEFAULT_PROFILES_HEADERS,
                        Ok = true,
                        Redirected = false,
                        Status = 200,
                        StatusText = "200",
                        URL = commonArguments.URL,
                        Data = body,
                        Type = "basic",
                    };

                    return result;
                }
            }

            return await webRequestController.PostAsync<GenerateResponseOp<GenericPostRequest>, ISimpleFetchApi.Response>(commonArguments, new GenerateResponseOp<GenericPostRequest>(), args, ct, reportData, webRequestHeader);
        }

        /// <summary>
        ///     Allocation-free check if URL ends with "lambdas/profiles" with or without trailing slash.
        /// </summary>
        private static bool IsProfilesEndpoint(string url)
        {
            ReadOnlySpan<char> urlSpan = url.AsSpan();

            // Remove trailing slash if present
            if (urlSpan.Length > 0 && urlSpan[^1] == '/')
                urlSpan = urlSpan[..^1];

            return urlSpan.EndsWith(PROFILES_SUFFIX.AsSpan(), StringComparison.Ordinal);
        }

        [Serializable]
        private struct ProfilesPostRequest
        {
            public List<string> ids;
        }

        internal struct GenerateResponseOp<TGenericRequest> : IWebRequestOp<TGenericRequest, ISimpleFetchApi.Response>
            where TGenericRequest: struct, GenericDownloadHandlerUtils.IGenericDownloadHandlerRequest, ITypedWebRequest
        {
            public UniTask<ISimpleFetchApi.Response> ExecuteAsync(TGenericRequest request, CancellationToken ct)
            {
                UnityWebRequest unityWebRequest = request.UnityWebRequest;
                string responseData = unityWebRequest.downloadHandler?.text ?? string.Empty;
                Dictionary<string, string>? responseHeadersDictionary = unityWebRequest.GetResponseHeaders();
                bool requestOk = unityWebRequest.result == UnityWebRequest.Result.Success;
                bool requestRedirected = unityWebRequest.result is UnityWebRequest.Result.ProtocolError or UnityWebRequest.Result.ConnectionError;
                int requestStatus = (int)unityWebRequest.responseCode;
                string? requestStatusText = unityWebRequest.responseCode.ToString();
                string requestUrl = unityWebRequest.url.EnsureNotNull();

                var result = new ISimpleFetchApi.Response
                {
                    Headers = responseHeadersDictionary,
                    Ok = requestOk,
                    Redirected = requestRedirected,
                    Status = requestStatus,
                    StatusText = requestStatusText,
                    URL = requestUrl,
                    Data = responseData,
                    Type = "basic", //Handle Response Types properly  type ResponseType = 'basic' | 'cors' | 'default' | 'error' | 'opaque' | 'opaqueredirect'
                };

                return UniTask.FromResult(result);
            }
        }
    }
}
