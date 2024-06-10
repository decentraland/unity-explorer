using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.Groups;
using ECS.StreamableLoading;

namespace DCL.SDKComponents.AudioSources
{
    /// <summary>
    ///     AudioSource systems create intentions to load audio clips so they should be executed before
    ///     Streamable Loading Group
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class SDKAudioSourceGroup { }
}
