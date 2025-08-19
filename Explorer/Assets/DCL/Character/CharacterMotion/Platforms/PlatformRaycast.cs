using DCL.Character;
using DCL.CharacterMotion.Components;
using RichTypes;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformRaycast
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterObject characterObject, CharacterPlatformComponent platformComponent, Transform characterTransform)
        {
            Option<CurrentPlatform> standing = characterObject.StandingGround;

            if (standing.Has == false)
            {
                platformComponent.CurrentPlatform = Option<CurrentPlatform>.None;
                platformComponent.LastPlatformPosition = null;
            }
            else if (platformComponent.CurrentPlatform.Has == false
                     || platformComponent.CurrentPlatform.Value.Transform != standing.Value.Transform)
            {
                platformComponent.CurrentPlatform = standing;
                platformComponent.LastPlatformPosition = null;
                platformComponent.LastAvatarRelativePosition = platformComponent.CurrentPlatform.Value.Transform.InverseTransformPoint(characterTransform.position);
                platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.Value.Transform.InverseTransformDirection(characterTransform.forward);
            }
        }
    }
}
