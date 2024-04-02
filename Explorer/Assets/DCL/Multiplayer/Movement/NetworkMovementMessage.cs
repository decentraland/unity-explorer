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

        public AnimationStates animState;
        public bool isStunned;

        public override string ToString() =>
            JsonUtility.ToJson(this);
    }
}
