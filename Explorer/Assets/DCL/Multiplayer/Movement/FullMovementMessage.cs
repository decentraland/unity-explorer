using System;
using UnityEngine;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct FullMovementMessage
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
