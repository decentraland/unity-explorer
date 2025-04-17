using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.Global;
using DCL.WebRequests.Analytics;

namespace DCL.WebRequests
{
    public class WebRequestsPlugin : IDCLGlobalPlugin
    {
        private readonly WebRequestsAnalyticsContainer analyticsContainer;

        public WebRequestsPlugin(WebRequestsAnalyticsContainer analyticsContainer)
        {
            this.analyticsContainer = analyticsContainer;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ShowWebRequestsAnalyticsSystem.InjectToWorld(ref builder, analyticsContainer, analyticsContainer.Widget);
        }
    }
}
