using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using System.Threading;

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

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

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
