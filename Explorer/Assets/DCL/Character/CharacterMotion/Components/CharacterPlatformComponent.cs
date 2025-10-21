using System;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public class CharacterPlatformComponent
    {
        public Transform? CurrentPlatform;
        public Collider? PlatformCollider;

        public Vector3? LastPlatformPosition;
        public Quaternion? LastPlatformRotation;
        public uint LastUpdateTick;
        public bool PositionChanged;
        public bool RotationChanged;
        public int FramesUngrounded;

        // Position and Rotation is local relative to the current platform, so if next frame the platform moves but the player doesn't, we can calculate what's the next world position for the character.
        // If the character moves, we update these local positions to save the new relative position
        public Vector3 LastAvatarRelativePosition;
        public Vector3 LastAvatarRelativeRotation;

        public void ClearPositionState()
        {
            LastPlatformPosition = null;
            PositionChanged = false;
        }

        public void ClearRotationState()
        {
            LastPlatformRotation = null;
            RotationChanged = false;
        }
    }
}
