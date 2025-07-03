using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Profiling;
using DCL.WebRequests.Analytics.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;
using Utility;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsAnalyticsContainer : IWebRequestsAnalyticsContainer
    {
        private static readonly MetricRegistration.RequestType[] SUPPORTED_REQUESTS =
        {
            new (typeof(GetAssetBundleWebRequest), "Asset Bundle"),
            new (typeof(GenericGetRequest), "Get"),

            new (typeof(PartialDownloadRequest), "Partial"),
            new (typeof(GenericPostRequest), "Post"),
            new (typeof(GenericPutRequest), "Put"),
            new (typeof(GenericPatchRequest), "Patch"),
            new (typeof(GenericHeadRequest), "Head"),
            new (typeof(GenericDeleteRequest), "Delete"),
            new (typeof(GetTextureWebRequest), "Texture"),
            new (typeof(GetAudioClipWebRequest), "Audio"),
        };

        private static readonly MetricRegistration.MetricType[] METRICS =
        {
            new (typeof(ActiveCounter)),
            new (typeof(TotalCounter), MetricAggregationMode.SUM),
            new (typeof(TotalFailed)),
            new (typeof(CannotConnectCounter), MetricAggregationMode.SUM),
            new (typeof(BandwidthDown), new MetricRegistration.MetricAggregationType(MetricAggregationMode.SUM, NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED), new MetricRegistration.MetricAggregationType(MetricAggregationMode.SUM_PER_FRAME, NetworkProfilerCounters.WEB_REQUESTS_DOWNLOADED_FRAME)),
            new (typeof(BandwidthUp), new MetricRegistration.MetricAggregationType(MetricAggregationMode.SUM, NetworkProfilerCounters.WEB_REQUESTS_UPLOADED), new MetricRegistration.MetricAggregationType(MetricAggregationMode.SUM_PER_FRAME, NetworkProfilerCounters.WEB_REQUESTS_UPLOADED_FRAME)),
            new (typeof(ServeTimeSmallFileAverage)),
            new (typeof(ServeTimePerMBAverage)),
            new (typeof(FillRateAverage)),
            new (typeof(TimeToFirstByteAverage)),
        };

        public DebugWidgetBuilder? Widget { get; private set; }

        private readonly Dictionary<Type, IReadOnlyList<MetricRegistration>> createdMetrics = new (SUPPORTED_REQUESTS.Length);

        private readonly List<MetricRegistration.AggregatedMetric> aggregatedMetrics = new (EnumUtils.Values<MetricAggregationMode>().Length);

        public static WebRequestsAnalyticsContainer Create(DebugWidgetBuilder? debugWidgetBuilder)
        {
            var container = new WebRequestsAnalyticsContainer { Widget = debugWidgetBuilder };

            Dictionary<Type, IReadOnlyList<MetricRegistration>> createdMetrics = container.createdMetrics;

            var aggregatedMetrics = new Dictionary<Type, List<IRequestMetric>>();

            // Create an instance of every metric and pair it with the request type
            foreach (MetricRegistration.RequestType requestType in SUPPORTED_REQUESTS)
            {
                var registrations = new List<MetricRegistration>(METRICS.Length);

                // Create an instance of the metric for each metric type

                foreach (MetricRegistration.MetricType metricType in METRICS)
                {
                    var instance = (IRequestMetric)Activator.CreateInstance(metricType.Type);

                    ElementBinding<ulong>? binding = null;

                    // Create a debug marker for each metric
                    if (debugWidgetBuilder != null)
                    {
                        binding = new ElementBinding<ulong>(0);
                        debugWidgetBuilder.AddMarker(requestType.MarkerName + "-" + metricType.Type.Name, binding, instance.GetUnit());
                    }

                    var registration = new MetricRegistration(instance, binding);
                    registrations.Add(registration);

                    // Add to the aggregated metrics
                    if (metricType.AggregationModes.Length > 0)
                    {
                        if (!aggregatedMetrics.TryGetValue(metricType.Type, out List<IRequestMetric>? list))
                        {
                            list = new List<IRequestMetric>();
                            aggregatedMetrics.Add(metricType.Type, list);
                        }

                        list.Add(instance);
                    }
                }

                createdMetrics.Add(requestType.Type, registrations);
            }

            foreach (MetricRegistration.MetricType metricType in METRICS)
            {
                foreach (MetricRegistration.MetricAggregationType aggregationType in metricType.AggregationModes)
                {
                    if (!aggregatedMetrics.TryGetValue(metricType.Type, out List<IRequestMetric>? list)) continue;

                    ElementBinding<ulong>? binding = null;

                    if (debugWidgetBuilder != null)
                    {
                        binding = new ElementBinding<ulong>(0);
                        debugWidgetBuilder.AddMarker($"{aggregationType.AggregationMode}-{metricType.Type.Name}", binding, list[0].GetUnit());
                    }

                    var aggregatedMetric = new MetricRegistration.AggregatedMetric(list, aggregationType, binding);
                    container.aggregatedMetrics.Add(aggregatedMetric);
                }
            }

            return container;
        }

        public IReadOnlyList<MetricRegistration> GetTrackedMetrics() =>
            createdMetrics.SelectMany(c => c.Value).ToList();

        public IReadOnlyList<MetricRegistration.AggregatedMetric> GetAggregatedMetrics() =>
            aggregatedMetrics;

        void IWebRequestsAnalyticsContainer.OnRequestStarted(ITypedWebRequest request, IWebRequest webRequest)
        {
            Type type = request.GetType();

            if (!createdMetrics.TryGetValue(type, out IReadOnlyList<MetricRegistration>? metrics))
                return;

            foreach (MetricRegistration registration in metrics)
                registration.metric.OnRequestStarted(request, webRequest);
        }

        void IWebRequestsAnalyticsContainer.OnRequestFinished(ITypedWebRequest request, IWebRequest webRequest)
        {
            Type type = request.GetType();

            if (!createdMetrics.TryGetValue(type, out IReadOnlyList<MetricRegistration>? metrics))
                return;

            foreach (MetricRegistration registration in metrics)
                registration.metric.OnRequestEnded(request, webRequest);
        }
    }
}
