using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    public abstract class RequestMetricBase
    {
        private ElementBinding<ulong>? basicValueBinding;

        /// <summary>
        ///     Should be updated every frame
        /// </summary>
        public virtual void Update() { }

        public abstract DebugLongMarkerDef.Unit GetUnit();

        public abstract ulong GetMetric();

        public abstract void OnRequestStarted(ITypedWebRequest request, DateTime startTime);

        public abstract void OnRequestEnded(ITypedWebRequest request, TimeSpan duration);

        public virtual void CreateDebugMenu(DebugWidgetBuilder? builder, IWebRequestsAnalyticsContainer.RequestType requestType)
        {
            if (builder == null) return;

            basicValueBinding = new ElementBinding<ulong>(0);
            builder.AddMarker(requestType.MarkerName + "-" + GetType().Name, basicValueBinding, GetUnit());
        }

        /// <summary>
        ///     Updates Debug Bindings
        /// </summary>
        public virtual void UpdateDebugMenu()
        {
            if (basicValueBinding != null)
                basicValueBinding.Value = GetMetric();
        }
    }
}
