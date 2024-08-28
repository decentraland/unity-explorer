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

        public int tier;

        public override string ToString() =>
            JsonUtility.ToJson(this);

        public bool Equals(NetworkMovementMessage other) =>
            timestamp.Equals(other.timestamp) && position.Equals(other.position) && velocity.Equals(other.velocity) && animState.Equals(other.animState) && isStunned == other.isStunned;

        public override bool Equals(object obj) =>
            obj is NetworkMovementMessage other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(timestamp, position, velocity, animState, isStunned);
    }
}
