using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class SaveLocalPosition
    {
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 position)
        {
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastPosition = platformComponent.CurrentPlatform.transform.InverseTransformPoint(position);
        }
    }
}
