using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public static class AvatarPoolUtils
    {
        public static AvatarBase AvatarBasePrefab;

        public static AvatarBase CreateAvatarContainer() =>
            Object.Instantiate(AvatarBasePrefab, Vector3.zero, Quaternion.identity);
    }
}
