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
        AnalyticsConfiguration Configuration { get; }

        void SetCommonParam(IRealmData realmData, IWeb3IdentityCache identityCache, ExposedTransform playerTransform);
        void Track(string eventName, JsonObject properties = null);

        public static IAnalyticsController Null => NullAnalytics.Instance;

        private sealed class NullAnalytics : IAnalyticsController
        {
            private NullAnalytics() {}

            private static readonly Lazy<NullAnalytics> INSTANCE = new (() => new NullAnalytics());

            public static IAnalyticsController Instance => INSTANCE.Value;

            public AnalyticsConfiguration Configuration => ScriptableObject.CreateInstance<AnalyticsConfiguration>();

            public void SetCommonParam(IRealmData _, IWeb3IdentityCache __, ExposedTransform ___) { }
            public void Track(string _, JsonObject __ = null) { }
        }
    }
}
