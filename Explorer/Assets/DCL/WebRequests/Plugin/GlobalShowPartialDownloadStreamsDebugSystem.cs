using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities.UIBindings;
using DCL.WebRequests.HTTP2;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GlobalShowPartialDownloadStreamsDebugSystem : ShowPartialDownloadStreamsDebugSystem
    {
        public GlobalShowPartialDownloadStreamsDebugSystem(World world, Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>> bindings, DebugWidgetVisibilityBinding visibilityBinding) : base(world, bindings, visibilityBinding) { }

        public override void BeforeUpdate(in float t)
        {
            // Reset all bindings before updating
            foreach (ElementBinding<ulong>? binding in bindings.Values) binding.Value = 0;
        }
    }
}
