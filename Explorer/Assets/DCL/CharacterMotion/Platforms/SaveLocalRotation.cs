using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public class SaveLocalRotation
    {
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 forward)
        {
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastRotation =
                    platformComponent.CurrentPlatform.transform.InverseTransformDirection(forward);
        }
    }
}
