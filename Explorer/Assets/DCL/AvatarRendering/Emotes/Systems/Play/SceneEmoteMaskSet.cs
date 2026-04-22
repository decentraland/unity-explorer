using DCL.ECSComponents;
using UnityEngine;

namespace DCL.AvatarRendering.Emotes.Play
{
    /// <summary>
    /// Inspector-pluggable mapping from <see cref="AvatarEmoteMask"/> to <see cref="AvatarMask"/>.
    /// Used by the scene-emote Playable-graph playback fork (local-scene-development, player builds).
    /// Instance lives at Resources/SceneEmoteMaskSet.asset so it can be loaded via Resources.Load,
    /// mirroring how BaseAnimatorController is loaded in LoadGLTFSystem.
    /// </summary>
    [CreateAssetMenu(menuName = "DCL/Avatar/Scene Emote Mask Set", fileName = "SceneEmoteMaskSet")]
    public class SceneEmoteMaskSet : ScriptableObject
    {
        [SerializeField] private AvatarMask? fullBody;
        [SerializeField] private AvatarMask? upperBody;

        public AvatarMask? Get(AvatarEmoteMask mask) =>
            mask switch
            {
                AvatarEmoteMask.AemFullBody => fullBody,
                AvatarEmoteMask.AemUpperBody => upperBody,
                _ => null,
            };
    }
}
