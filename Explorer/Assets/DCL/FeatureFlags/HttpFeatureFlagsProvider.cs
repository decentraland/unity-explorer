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
            urlBuilder.Clear();

            URLAddress fetchUrl = urlBuilder.AppendDomain(options.URL)
                                            .AppendPath(URLPath.FromString($"{options.AppName}.json"))
                                            .Build();

            headers.Clear();
            headers["X-Debug"] = options.Debug ? "true" : "false";
			headers["referer"] = options.Hostname;

            if (options.UserId.HasValue)
                headers["X-Address-Hash"] = options.UserId;

            var result = webRequestController.GetAsync(new CommonArguments(fetchUrl), ct, ReportCategory.FEATURE_FLAGS,
                new WebRequestHeadersInfo(headers));

            FeatureFlagsResultDto response = await result.CreateFromJson<FeatureFlagsResultDto>(WRJsonParser.Newtonsoft);

            response = StripAppNameFromKeys(options.AppName, response);

            var config = new FeatureFlagsConfiguration(response);

            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(config);

            return config;
        }

        /// <summary>
        ///     Drops the app-name prefix (e.g. "explorer-") from flag and variant keys so they match the codebase
        ///     flag constants. Shared by the regular fetch and any standalone fetch (e.g.
        ///     <see cref="DeepLinkWorldWhitelistProvider" />). Null-safe against a partially populated document.
        /// </summary>
        public static FeatureFlagsResultDto StripAppNameFromKeys(string name, FeatureFlagsResultDto response)
        {
            if (response.flags != null)
            {
                Dictionary<string, bool> flags = new ();

                foreach ((string key, bool value) in response.flags)
                    flags[key.Replace($"{name}-", "", StringComparison.OrdinalIgnoreCase)] = value;

                response.flags = flags;
            }

            if (response.variants != null)
            {
                Dictionary<string, FeatureFlagVariantDto> variants = new ();

                foreach ((string key, FeatureFlagVariantDto value) in response.variants)
                    variants[key.Replace($"{name}-", "", StringComparison.OrdinalIgnoreCase)] = value;

                response.variants = variants;
            }

            return response;
        }
    }
}
