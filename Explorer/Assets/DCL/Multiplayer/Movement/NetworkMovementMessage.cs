using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct NetworkMovementMessage : IEquatable<NetworkMovementMessage>
    {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;
        public float velocitySqrMagnitude;

        public float rotationY;

        public MovementKind movementKind;
        public bool isSliding;
        public bool isStunned;

        public AnimationStates animState;

        public byte velocityTier;

        public NetworkMovementMessage(float timestamp, Vector3 position, Vector3 velocity, float velocitySqrMagnitude, float rotationY,
            MovementKind movementKind, bool isSliding, bool isStunned, AnimationStates animState, byte velocityTier)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.velocity = velocity;
            this.velocitySqrMagnitude = velocitySqrMagnitude;
            this.rotationY = rotationY;
            this.movementKind = movementKind;
            this.isSliding = isSliding;
            this.isStunned = isStunned;
            this.animState = animState;
            this.velocityTier = velocityTier;
        }

        public override string ToString() =>
            JsonUtility.ToJson(this)!;

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(timestamp);
            hashCode.Add(position);
            hashCode.Add(velocity);
            hashCode.Add(velocitySqrMagnitude);
            hashCode.Add(rotationY);
            hashCode.Add((int)movementKind);
            hashCode.Add(isSliding);
            hashCode.Add(isStunned);
            hashCode.Add(animState);
            hashCode.Add(velocityTier);
            return hashCode.ToHashCode();
        }

        public bool Equals(NetworkMovementMessage other) =>
            timestamp.Equals(other.timestamp)
            && position.Equals(other.position)
            && velocity.Equals(other.velocity)
            && velocitySqrMagnitude.Equals(other.velocitySqrMagnitude)
            && rotationY.Equals(other.rotationY)
            && movementKind == other.movementKind
            && isSliding == other.isSliding
            && isStunned == other.isStunned
            && animState.Equals(other.animState)
            && velocityTier == other.velocityTier;

        public override bool Equals(object obj) =>
            obj is NetworkMovementMessage other && Equals(other);
    }
}
