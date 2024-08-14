using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct NetworkMovementMessage
    {
        public float enqueueTime;

        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;

        public MovementKind movementKind;
        public bool isSliding;
        public bool isStunned;

        // public bool isGrounded;
        // public bool isJumping;
        // public bool isLongJump;
        // public bool isLongFall;
        // public bool isFalling;
        //
        public AnimationStates animState;

        public override string ToString() =>
            JsonUtility.ToJson(this);
    }
}
