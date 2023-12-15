﻿using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public class CharacterPlatformComponent
    {
        public Transform CurrentPlatform;

        // Position and Rotation is local relative to the current platform, so if next frame the platform moves but the player doesn't, we can calculate what's the next world position for the character.
        // If the character moves, we update these local positions to save the new relative position
        public Vector3 LastPosition;
        public Vector3 LastRotation;
    }
}
