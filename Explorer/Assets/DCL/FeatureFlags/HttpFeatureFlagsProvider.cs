using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.FeatureFlags
{
    public class HttpFeatureFlagsProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, string> headers = new ();

        public HttpFeatureFlagsProvider(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<FeatureFlagsConfiguration> GetAsync(FeatureFlagOptions options, CancellationToken ct)
        {
UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:24"); // SPECIAL_DEBUG_LINE_STATEMENT
            urlBuilder.Clear();

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:27"); // SPECIAL_DEBUG_LINE_STATEMENT
            URLAddress fetchUrl = urlBuilder.AppendDomain(options.URL)
                                            .AppendPath(URLPath.FromString($"{options.AppName}.json"))
                                            .Build();

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:32"); // SPECIAL_DEBUG_LINE_STATEMENT
            headers.Clear();
            headers["X-Debug"] = options.Debug ? "true" : "false";
			headers["referer"] = options.Hostname;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:37"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (options.UserId.HasValue)
                headers["X-Address-Hash"] = options.UserId;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:41"); // SPECIAL_DEBUG_LINE_STATEMENT
            var result = webRequestController.GetAsync(new CommonArguments(fetchUrl), ct, ReportCategory.FEATURE_FLAGS,
                new WebRequestHeadersInfo(headers));

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:45"); // SPECIAL_DEBUG_LINE_STATEMENT
            FeatureFlagsResultDto response = await result.CreateFromJson<FeatureFlagsResultDto>(WRJsonParser.Newtonsoft);

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:48"); // SPECIAL_DEBUG_LINE_STATEMENT
            response = StripAppNameFromKeys(options.AppName, response);

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:51"); // SPECIAL_DEBUG_LINE_STATEMENT
            var config = new FeatureFlagsConfiguration(response);

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:54"); // SPECIAL_DEBUG_LINE_STATEMENT
            FeatureFlagsConfiguration.Initialize(config);

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:57"); // SPECIAL_DEBUG_LINE_STATEMENT
            return config;
        }

        private static FeatureFlagsResultDto StripAppNameFromKeys(string name, FeatureFlagsResultDto response)
        {
UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:63"); // SPECIAL_DEBUG_LINE_STATEMENT
            Dictionary<string, bool> flags = new ();

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:66"); // SPECIAL_DEBUG_LINE_STATEMENT
            foreach ((string key, bool value) in response.flags)
                flags[key.Replace($"{name}-", "", StringComparison.OrdinalIgnoreCase)] = value;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:70"); // SPECIAL_DEBUG_LINE_STATEMENT
            response.flags = flags;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:73"); // SPECIAL_DEBUG_LINE_STATEMENT
            Dictionary<string, FeatureFlagVariantDto> variants = new ();

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:76"); // SPECIAL_DEBUG_LINE_STATEMENT
            foreach ((string key, FeatureFlagVariantDto value) in response.variants)
                variants[key.Replace($"{name}-", "")] = value;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:80"); // SPECIAL_DEBUG_LINE_STATEMENT
            response.variants = variants;

UnityEngine.Debug.Log("HttpFeatureFlagsProvider.cs:83"); // SPECIAL_DEBUG_LINE_STATEMENT
            return response;
        }
    }
}
