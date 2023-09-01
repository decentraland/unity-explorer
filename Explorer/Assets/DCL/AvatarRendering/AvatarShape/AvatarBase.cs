using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape
{
    public class AvatarBase : MonoBehaviour
    {
        [field: SerializeField]
        public SkinnedMeshRenderer AvatarSkinnedMeshRenderer { get; private set; }
    }
}
