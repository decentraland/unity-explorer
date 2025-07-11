﻿using DCL.CharacterMotion.Components;
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
        public bool isInstant;
        public bool isEmoting;

        public AnimationStates animState;

        public byte velocityTier;

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
            hashCode.Add(isInstant);
            hashCode.Add(isEmoting);
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
            && velocityTier == other.velocityTier
            && isInstant == other.isInstant
            && isEmoting == other.isEmoting;

        public override bool Equals(object obj) =>
            obj is NetworkMovementMessage other && Equals(other);
    }
}
