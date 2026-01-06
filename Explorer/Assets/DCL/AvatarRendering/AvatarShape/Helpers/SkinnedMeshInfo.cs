#if UNITY_EDITOR
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    /// <summary>
    /// Add this component to a game object during play time to see which
    /// transforms are affecting a given skinned mesh renderer. All the
    /// functionality is in the custom editor for this component.
    /// </summary>
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class SkinnedMeshInfo : MonoBehaviour
    {
    }
}
#endif
