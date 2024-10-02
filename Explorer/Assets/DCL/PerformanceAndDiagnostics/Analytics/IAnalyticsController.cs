using DCL.Multiplayer.HealthChecks;
using DCL.Web3.Identities;
using ECS;
using Segment.Serialization;
using System;
using UnityEngine;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public interface IAnalyticsController
    {
        public const string UNDEFINED = "UNDEFINED";

        AnalyticsConfiguration Configuration { get; }

        void SetCommonParam(IRealmData realmData, IWeb3IdentityCache? identityCache, IExposedTransform playerTransform);

        void Track(string eventName, JsonObject? properties = null);

        void Identify(IWeb3Identity? identity);

        void Flush();

        public static IAnalyticsController Null => NullAnalytics.Instance;

        private sealed class NullAnalytics : IAnalyticsController
        {
            private NullAnalytics() { }

            private static readonly Lazy<NullAnalytics> INSTANCE = new (() => new NullAnalytics());

            public static IAnalyticsController Instance => INSTANCE.Value;

            public AnalyticsConfiguration Configuration => ScriptableObject.CreateInstance<AnalyticsConfiguration>();

            public void SetCommonParam(IRealmData _, IWeb3IdentityCache? __, IExposedTransform ___) { }

            public void Track(string _, JsonObject? __ = null) { }

            public void Identify(IWeb3Identity? _) { }

            public void Flush() { }
        }
    }

    public static class AnalyticsExtensions
    {
        public static IHealthCheck WithFailAnalytics(this IHealthCheck origin, IAnalyticsController analyticsController) =>
            new FailAnalyticsHealthCheckDecorator(origin, analyticsController);
    }
}
