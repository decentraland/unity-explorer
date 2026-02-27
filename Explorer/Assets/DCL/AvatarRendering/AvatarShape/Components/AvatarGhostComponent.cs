using System;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Controls the ghost renderer visibility on <see cref="UnityInterface.AvatarBase" />.
    ///     When enabled, the ghost is shown (e.g. while the avatar is loading). When disabled, the full avatar is shown.
    /// </summary>
    public struct AvatarGhostComponent
    {
        public bool Enabled;
        public readonly GameObject Ghost;

        public AvatarGhostComponent(GameObject ghost)
        {
            Enabled = true;
            Ghost = ghost;
        }

        public void Disable()
        {
            Ghost.SetActive(false);
            Enabled = false;
        }
    }
}
