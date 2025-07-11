using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.DebugUtilities.UIBindings;
using DCL.WebRequests.HTTP2;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Must be executed in every world
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    public partial class ShowPartialDownloadStreamsDebugSystem : BaseUnityLoopSystem
    {
        protected readonly Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>> bindings;
        protected readonly DebugWidgetVisibilityBinding visibilityBinding;

        public ShowPartialDownloadStreamsDebugSystem(World world, Dictionary<Http2PartialDownloadDataStream.Mode, ElementBinding<ulong>> bindings, DebugWidgetVisibilityBinding visibilityBinding) : base(world)
        {
            this.bindings = bindings;
            this.visibilityBinding = visibilityBinding;
        }

        protected override void Update(float t)
        {
            if (!visibilityBinding.IsExpanded)
                return;

            GatherStreamStatsQuery(World);
        }

        [Query]
        private void GatherStreamStats(StreamableLoadingState streamableLoadingState)
        {
            PartialLoadingState? partialData = streamableLoadingState.PartialDownloadingData;

            if (partialData == null)
                return;

            if (partialData.Value.PartialDownloadStream is not Http2PartialDownloadDataStream http2PartialDownloadStream)
                return;

            bindings[http2PartialDownloadStream.OpMode].Value++;
        }
    }
}
