using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsPlugin : IDCLGlobalPlugin
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public WebRequestsPlugin(IWebRequestsAnalyticsContainer analyticsContainer, IDebugContainerBuilder debugContainerBuilder)
        {
            this.analyticsContainer = analyticsContainer;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ShowWebRequestsAnalyticsSystem.InjectToWorld(ref builder, analyticsContainer, debugContainerBuilder, new[]
            {
                typeof(GenericGetRequest),
                typeof(GenericPostRequest),
                typeof(GenericPutRequest),
                typeof(GenericPatchRequest),
                typeof(GetTextureWebRequest),
            });
        }
    }
}
