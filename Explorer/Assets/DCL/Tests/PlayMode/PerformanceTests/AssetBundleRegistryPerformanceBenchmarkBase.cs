using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.Tests.PlayMode.PerformanceTests
{
    public abstract class AssetBundleRegistryPerformanceBenchmarkBase : PerformanceBenchmark
    {
        protected static readonly object[] TEST_CASES_SOURCE =
        {
            new object[] { 1, 10, 0.25d, 100 },
            new object[] { 10, 10, 0.25d, 100 },
            new object[] { 25, 10, 0.25d, 100 },
            new object[] { 50, 10, 0.25d, 100 },
            new object[] { 20, 10, 6, 50 },
            new object[] { 20, 5, 20, 50 },
        };

        [Serializable]
        public class GenericEntityDefinition : EntityDefinitionBase { }

        private const string ENTITIES_ACTIVE = "entities/active/";

        private readonly URLAddress entitiesActive;

        protected AssetBundleRegistryPerformanceBenchmarkBase(string assetBundleRegistryUrl)
        {
            entitiesActive = URLAddress.FromString(assetBundleRegistryUrl + ENTITIES_ACTIVE);
        }

        protected async Task GetEntitiesActiveAsync(int concurrency, int iterations, double delayBetweenIterations, int totalRequests,
            string[] pointers)
        {
            CreateController(concurrency);

            var delay = TimeSpan.FromSeconds(delayBetweenIterations);

            var bodyBuilder = new StringBuilder();

            bodyBuilder.Append("{\"pointers\":[");

            for (int i = 0; i < pointers.Length; ++i)
            {
                string pointer = pointers[i];

                // String Builder has overloads for int to prevent allocations
                bodyBuilder.Append('\"');
                bodyBuilder.Append(pointer);
                bodyBuilder.Append(',');
                bodyBuilder.Append(pointer);
                bodyBuilder.Append('\"');

                if (i != pointers.Length - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            var body = GenericPostArguments.CreateJson(bodyBuilder.ToString());

            await BenchmarkAsync(_ => controller!.PostAsync(new CommonArguments(entitiesActive, RetryPolicy.NONE), body, CancellationToken.None, ReportCategory.GENERIC_WEB_REQUEST)
                                                 .CreateFromJson<List<GenericEntityDefinition>>(WRJsonParser.Newtonsoft), new[] { "" }, 1, totalRequests, iterations, delay);
        }
    }
}
