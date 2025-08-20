using DCL.Character;
using RichTypes;
using UnityEngine;
using UnityEngine.Assertions;

namespace DCL.CharacterMotion.Components
{
    public class CharacterPlatformComponent
    {
        private Vector3? lastPlatformDelta;
        private Vector3? lastPlatformPosition;
        private Quaternion? lastPlatformRotation;

        public Option<CurrentPlatform> CurrentPlatform { get; private set; }

        public bool IsMovingPlatform { get; private set; }
        public bool IsRotatingPlatform { get; private set; }
        public int FramesUngrounded;

        // Position and Rotation is local relative to the current platform, so if next frame the platform moves but the player doesn't, we can calculate what's the next world position for the character.
        // If the character moves, we update these local positions to save the new relative position
        public Vector3 LastAvatarRelativePosition { get; private set; }
        public Vector3 LastAvatarRelativeRotation { get; private set; }

        public void ApplyPlatform(CurrentPlatform platform, Transform characterTransform)
        {
            CurrentPlatform = Option<CurrentPlatform>.Some(platform);
            lastPlatformPosition = null;
            ApplyAvatarRelativeData(characterTransform);
        }

        public void ResetPlatform()
        {
            CurrentPlatform = Option<CurrentPlatform>.None;
            lastPlatformPosition = null;
        }

        private void ApplyAvatarRelativeData(Transform characterTransform)
        {
            Assert.IsTrue(CurrentPlatform.Has, "Current platform must exist to execute this operation");
            Transform platform = CurrentPlatform.Value.Transform;
            LastAvatarRelativePosition = platform.InverseTransformPoint(characterTransform.position);
            LastAvatarRelativeRotation = platform.InverseTransformDirection(characterTransform.forward);
        }

        private void ApplyAvatarRelativeRotation(Vector3 forward)
        {
            Assert.IsTrue(CurrentPlatform.Has, "Current platform must exist to execute this operation");
            Transform platform = CurrentPlatform.Value.Transform;
            LastAvatarRelativeRotation = platform.InverseTransformDirection(forward);
        }

        private void ApplyAvatarRelativePosition(Vector3 position)
        {
            Assert.IsTrue(CurrentPlatform.Has, "Current platform must exist to execute this operation");
            Transform platform = CurrentPlatform.Value.Transform;
            LastAvatarRelativePosition = platform.InverseTransformPoint(position);
        }

        private void ResetMovement()
        {
            lastPlatformPosition = null;
            lastPlatformDelta = null;
            IsMovingPlatform = false;
        }

        private void ResetRotation()
        {
            lastPlatformRotation = null;
            IsRotatingPlatform = false;
        }

        public void SaveLocalPosition(Vector3 characterPosition)
        {
            if (CurrentPlatform.Has)
            {
                Transform transform = CurrentPlatform.Value.Transform;
                Vector3 currentPlatformPosition = transform.position;

                if (lastPlatformPosition != null)
                {
                    lastPlatformDelta = lastPlatformPosition - currentPlatformPosition;
                    IsMovingPlatform = lastPlatformDelta.Value.sqrMagnitude > Mathf.Epsilon;
                }

                lastPlatformPosition = currentPlatformPosition;
                ApplyAvatarRelativePosition(characterPosition);
            }
            else
                ResetMovement();
        }

        public void SaveLocalRotation(Vector3 forward)
        {
            if (CurrentPlatform.Has)
            {
                Transform transform = CurrentPlatform.Value.Transform;
                var currentPlatformRotation = transform.rotation;

                if (lastPlatformRotation != null)
                {
                    float angleDifference = Quaternion.Angle(lastPlatformRotation.Value, currentPlatformRotation);
                    IsRotatingPlatform = angleDifference > Mathf.Epsilon;
                }

                lastPlatformRotation = currentPlatformRotation;
                ApplyAvatarRelativeRotation(forward);
            }
            else
                ResetRotation();
        }
    }
}
