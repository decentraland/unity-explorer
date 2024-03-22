using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement
{
    [Serializable]
    public struct FullMovementMessage
    {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;

        public CharacterAnimationComponent.AnimationStates animState;
        public bool isStunned;

        public override string ToString() =>
            JsonUtility.ToJson(this);
    }
}
