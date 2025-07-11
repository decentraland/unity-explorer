using Arch.Core;
using Arch.SystemGroups;
using Best.HTTP.Caching;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.HTTP2;
using ECS.LifeCycle;
using System.Collections.Generic;
using Utility;

namespace DCL.WebRequests
{
    public class WebRequestsPlugin : IDCLGlobalPlugin, IDCLWorldPlugin
    {
        private readonly WebRequestsAnalyticsContainer analyticsContainer;
        private readonly HTTPCache httpCache;

        private readonly ElementBinding<ulong> cacheSizeBinding = new (0);

        private readonly DebugWidgetVisibilityBinding? visibilityBinding;
        private readonly Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>>? debugBindings;

        public WebRequestsPlugin(WebRequestsAnalyticsContainer analyticsContainer, HTTPCache httpCache, IDebugContainerBuilder debugContainerBuilder)
        {
            this.analyticsContainer = analyticsContainer;
            this.httpCache = httpCache;

            DebugWidgetBuilder? widget = debugContainerBuilder.TryAddWidget(IDebugContainerBuilder.Categories.PARTIAL_DOWNLOAD);

            if (widget == null)
                return;

            widget.SetVisibilityBinding(visibilityBinding = new DebugWidgetVisibilityBinding(true));

            widget.AddMarker("Cache Size", cacheSizeBinding, DebugLongMarkerDef.Unit.Bytes);

            debugBindings = new Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>>();

            foreach (Http2PartialDownloadDataStream.Mode value in EnumUtils.Values<Http2PartialDownloadDataStream.Mode>())
            {
                var binding = new ElementBinding<ulong>(0);
                debugBindings[value] = binding;
                widget.AddMarker(value.ToString(), binding, DebugLongMarkerDef.Unit.NoFormat);
            }
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            if (debugBindings == null)
                return;

            ShowPartialDownloadStreamsDebugSystem.InjectToWorld(ref builder, debugBindings, visibilityBinding);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ShowWebRequestsAnalyticsSystem.InjectToWorld(ref builder, analyticsContainer, analyticsContainer.Widget);

            if (debugBindings != null)
                GlobalShowPartialDownloadStreamsDebugSystem.InjectToWorld(ref builder, debugBindings, visibilityBinding, httpCache, cacheSizeBinding);
        }
    }
}
