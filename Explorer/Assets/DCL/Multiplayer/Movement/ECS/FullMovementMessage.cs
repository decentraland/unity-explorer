using DCL.CharacterMotion.Components;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS
{
    [Serializable]
    public class FullMovementMessage
    {
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;

        public CharacterAnimationComponent.AnimationStates animState;
        public bool isStunned;
    }
}
