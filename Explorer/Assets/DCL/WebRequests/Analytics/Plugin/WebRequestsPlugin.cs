using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using DCL.WebRequests.GenericDelete;

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
            ShowWebRequestsAnalyticsSystem.InjectToWorld(ref builder, analyticsContainer, debugContainerBuilder, new ShowWebRequestsAnalyticsSystem.RequestType[]
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
            });
        }
    }
}
