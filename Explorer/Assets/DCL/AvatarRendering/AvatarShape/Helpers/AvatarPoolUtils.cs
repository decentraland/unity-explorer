using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Helpers
{
    public class AvatarPoolUtils
    {
        private readonly AvatarBase avatarBasePrefab;

        public AvatarPoolUtils(AvatarBase avatarBasePrefab)
        {
            this.avatarBasePrefab = avatarBasePrefab;
        }

        public AvatarBase CreateAvatarContainer() =>
            Object.Instantiate(avatarBasePrefab, Vector3.zero, Quaternion.identity);
    }
}
