using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.FeatureFlags
{
    public class HttpFeatureFlagsProvider : IFeatureFlagsProvider
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

            if (options.UserId.HasValue)
                headers["X-Address-Hash"] = options.UserId;

            var result = webRequestController.GetAsync(new CommonArguments(fetchUrl), ct, "FEATURE_FLAGS",
                new WebRequestHeadersInfo(headers));

            FeatureFlagsResultDto response = await result.CreateFromJson<FeatureFlagsResultDto>(WRJsonParser.Newtonsoft);

            response = RemoveAppNameFromKeys(options.AppName, response);

            return new FeatureFlagsConfiguration(response);
        }

        private static FeatureFlagsResultDto RemoveAppNameFromKeys(string name, FeatureFlagsResultDto response)
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
