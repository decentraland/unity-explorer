
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    [TrackClipType(typeof(TriggerEmotePlayableAsset))]
    [TrackClipType(typeof(MoveAvatarPlayableAsset))]
    [TrackBindingType(typeof(AvatarBase))]
    public class AvatarTrack : TrackAsset
    {

    }
}
