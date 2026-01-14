using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using DCL.WebRequests.ChromeDevtool;
using DCL.WebRequests.GenericDelete;
using System;

namespace DCL.WebRequests.Analytics
{
    public class WebRequestsPlugin : IDCLGlobalPlugin
    {
        private readonly IWebRequestsAnalyticsContainer analyticsContainer;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly ChromeDevtoolProtocolClient chromeDevtoolProtocolClient;
        private readonly bool isLocalSceneDevelopment;

        public WebRequestsPlugin(IWebRequestsAnalyticsContainer analyticsContainer, IDebugContainerBuilder debugContainerBuilder, ChromeDevtoolProtocolClient chromeDevtoolProtocolClient, bool isLocalSceneDevelopment)
        {
            this.analyticsContainer = analyticsContainer;
            this.debugContainerBuilder = debugContainerBuilder;
            this.chromeDevtoolProtocolClient = chromeDevtoolProtocolClient;
            this.isLocalSceneDevelopment = isLocalSceneDevelopment;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ShowWebRequestsAnalyticsSystem.RequestType[] types = isLocalSceneDevelopment
                ? Array.Empty<ShowWebRequestsAnalyticsSystem.RequestType>()
                : new ShowWebRequestsAnalyticsSystem.RequestType[]
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

            ShowWebRequestsAnalyticsSystem.InjectToWorld(ref builder, analyticsContainer, debugContainerBuilder, chromeDevtoolProtocolClient, types);
        }
    }
}
