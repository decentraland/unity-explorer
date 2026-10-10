using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;

namespace DCL.VoiceChat.Nearby.Systems
{
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(AvatarInstantiatorSystem))]
    public partial class NearbyVoiceChatGroup
    {
    }
}
