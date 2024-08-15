using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct NetworkMovementMessage
    {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;

        public MovementKind movementKind;
        public bool isSliding;
        public bool isStunned;

        public AnimationStates animState;

        public override string ToString() =>
            JsonUtility.ToJson(this);
    }
}
