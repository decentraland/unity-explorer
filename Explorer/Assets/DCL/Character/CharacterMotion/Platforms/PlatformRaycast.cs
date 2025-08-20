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
                platformComponent.ResetPlatform();
            else if (platformComponent.CurrentPlatform.Has == false
                     || platformComponent.CurrentPlatform.Value.Transform != standing.Value.Transform
                    )
                platformComponent.ApplyPlatform(standing.Value, characterTransform);
        }
    }
}
