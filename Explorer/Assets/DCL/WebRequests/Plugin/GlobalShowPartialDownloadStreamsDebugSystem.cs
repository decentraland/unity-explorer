using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Best.HTTP.Caching;
using DCL.DebugUtilities.UIBindings;
using DCL.WebRequests.HTTP2;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GlobalShowPartialDownloadStreamsDebugSystem : ShowPartialDownloadStreamsDebugSystem
    {
        private readonly ElementBinding<ulong> cacheSize;
        private readonly HTTPCache httpCache;

        public GlobalShowPartialDownloadStreamsDebugSystem(World world, Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>> bindings, DebugWidgetVisibilityBinding visibilityBinding, HTTPCache httpCache, ElementBinding<ulong> cacheSize) : base(world, bindings, visibilityBinding)
        {
            this.httpCache = httpCache;
            this.cacheSize = cacheSize;
        }

        public override void BeforeUpdate(in float t)
        {
            // Reset all bindings before updating
            foreach (ElementBinding<ulong>? binding in bindings.Values) binding.Value = 0;
        }

        protected override void Update(float t)
        {
            if (visibilityBinding.IsExpanded)
                cacheSize.Value = (ulong)httpCache.CacheSize;

            base.Update(t);
        }
    }
}
