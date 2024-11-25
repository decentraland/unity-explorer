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

        public static IAnalyticsController Null => NullAnalytics.Instance;

        void Initialize(IWeb3Identity? web3Identity);

        void SetCommonParam(IRealmData realmData, IWeb3IdentityCache? identityCache, IExposedTransform playerTransform);

        void Track(string eventName, JsonObject? properties = null);

        void Identify(IWeb3Identity? identity);

        void Flush();

        private sealed class NullAnalytics : IAnalyticsController
        {
            private static readonly Lazy<NullAnalytics> INSTANCE = new (() => new NullAnalytics());

            public static IAnalyticsController Instance => INSTANCE.Value;

            public AnalyticsConfiguration Configuration => ScriptableObject.CreateInstance<AnalyticsConfiguration>();

            private NullAnalytics() { }

            public void Initialize(IWeb3Identity? web3Identity) { }

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
