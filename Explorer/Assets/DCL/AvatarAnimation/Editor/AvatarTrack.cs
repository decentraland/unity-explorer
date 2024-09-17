
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using UnityEngine.Timeline;

namespace DCL.AvatarAnimation
{
    /// <summary>
    /// A timeline track intented for animating avatars.
    /// </summary>
    [TrackColor(0.0f, 0.0f, 1.0f)]
    [TrackClipType(typeof(TriggerEmotePlayableAsset))]
    [TrackClipType(typeof(MoveAvatarPlayableAsset))]
    [TrackClipType(typeof(TeleportAvatarPlayableAsset))]
    [TrackBindingType(typeof(AvatarBase))]
    public class AvatarTrack : TrackAsset
    {

    }
}
