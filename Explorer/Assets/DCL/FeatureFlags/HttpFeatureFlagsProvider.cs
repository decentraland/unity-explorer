using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.FeatureFlags
{
    public class HttpFeatureFlagsProvider : IFeatureFlagsProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly FeatureFlagsCache featureFlagsCache;
        private readonly URLBuilder urlBuilder = new ();
        private readonly Dictionary<string, string> headers = new ();

        public HttpFeatureFlagsProvider(IWebRequestController webRequestController,
            FeatureFlagsCache featureFlagsCache)
        {
            this.webRequestController = webRequestController;
            this.featureFlagsCache = featureFlagsCache;
        }

        public async UniTask<FeatureFlagsConfiguration> GetAsync(FeatureFlagOptions options, CancellationToken ct)
        {
            urlBuilder.Clear();

            URLAddress fetchUrl = urlBuilder.AppendDomain(options.URL)
                                            .AppendPath(URLPath.FromString($"{options.AppName}.json"))
                                            .Build();

            headers.Clear();
            headers["X-Debug"] = options.Debug ? "true" : "false";
			headers["referer"] = options.Hostname;

            if (options.UserId.HasValue)
                headers["X-Address-Hash"] = options.UserId;

            var result = webRequestController.GetAsync(new CommonArguments(fetchUrl), ReportCategory.FEATURE_FLAGS,
                new WebRequestHeadersInfo(headers));

            FeatureFlagsResultDto response = await result.CreateFromJson<FeatureFlagsResultDto>(WRJsonParser.Newtonsoft, ct);

            response = StripAppNameFromKeys(options.AppName, response);

            var config = new FeatureFlagsConfiguration(response);

            featureFlagsCache.Configuration = config;

            return config;
        }

        private static FeatureFlagsResultDto StripAppNameFromKeys(string name, FeatureFlagsResultDto response)
        {
            Dictionary<string, bool> flags = new ();

            foreach ((string key, bool value) in response.flags)
                flags[key.Replace($"{name}-", "", StringComparison.OrdinalIgnoreCase)] = value;

            response.flags = flags;

            Dictionary<string, FeatureFlagVariantDto> variants = new ();

            foreach ((string key, FeatureFlagVariantDto value) in response.variants)
                variants[key.Replace($"{name}-", "")] = value;

            response.variants = variants;

            return response;
        }
    }
}
