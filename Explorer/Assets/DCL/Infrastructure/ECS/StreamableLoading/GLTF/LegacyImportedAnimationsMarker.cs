using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    /// <summary>
    /// Attached by LoadGLTFSystem when a GLTF is imported with AnimationMethod.Legacy while
    /// the caller originally asked for Mecanim clips (scene-emote path in local-scene-development
    /// player builds — see LoadGLTFSystem.cs). Signals the playback layer to use a PlayableGraph
    /// route instead of reading clips from an AnimatorOverrideController.
    /// </summary>
    public class LegacyImportedAnimationsMarker : MonoBehaviour
    {
        public AnimationClip? AvatarClip;
        public AnimationClip? PropClip;
    }
}
