using DCL.Diagnostics;
using DCL.Web3.Identities;
using DCL.WebRequests.Analytics.Metrics;
using System;
using UnityEngine;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsContainer
    {
        public IWebRequestController WebRequestController { get; }

        public IWebRequestsAnalyticsContainer AnalyticsContainer { get; }

        private WebRequestsContainer(IWebRequestController webRequestController, IWebRequestsAnalyticsContainer analyticsContainer)
        {
            WebRequestController = webRequestController;
            AnalyticsContainer = analyticsContainer;
        }

        public static WebRequestsContainer Create(IWeb3IdentityCache web3IdentityProvider, WebRequestsContainerParams containerParams)
        {
            var analyticsContainer = new WebRequestsAnalyticsContainer()
                                    .AddTrackedMetric<ActiveCounter>()
                                    .AddTrackedMetric<Total>()
                                    .AddTrackedMetric<TotalFailed>()
                                    .AddTrackedMetric<BandwidthDown>()
                                    .AddTrackedMetric<BandwidthUp>();

            IWebRequestController webRequestController = new LogWebRequestController(
                new WebRequestController(analyticsContainer, web3IdentityProvider)
            );

            if (containerParams.ShouldApplyDelay())
            {
                ReportHub.Log(ReportCategory.WEB_REQUESTS, "Artificial delay enabled");
                webRequestController = new ArtificialDelayWebRequestController(webRequestController, containerParams.ArtificialDelaySeconds);
            }

            return new WebRequestsContainer(webRequestController, analyticsContainer);
        }
    }

    [Serializable]
    public class WebRequestsContainerParams
    {
        [SerializeField] private bool useDelay = true;
        [SerializeField] private float artificialDelaySeconds = 10;

        public float ArtificialDelaySeconds => artificialDelaySeconds;

        public bool ShouldApplyDelay() =>
            Application.isEditor
            && useDelay
            && Mathf.Approximately(artificialDelaySeconds, 0) == false;
    }
}
