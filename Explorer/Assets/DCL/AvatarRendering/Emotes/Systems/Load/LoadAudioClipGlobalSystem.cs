using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class LoadAudioClipGlobalSystem : LoadAudioClipSystem
    {
        internal LoadAudioClipGlobalSystem(World world, IStreamableCache<AudioClipData, GetAudioClipIntention> cache, IWebRequestController webRequestController) : base(world, cache, webRequestController) { }
    }
}
