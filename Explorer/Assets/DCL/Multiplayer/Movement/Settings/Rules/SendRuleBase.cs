using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.ECS;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public abstract class SendRuleBase : ScriptableObject
    {
        public Color color = Color.white;
        public bool IsEnabled  = true;
        public float MinTimeDelta = 0;
        public virtual string Message { get; set; }

        public abstract bool IsSendConditionMet(float t,
            FullMovementMessage lastFullMovementMessage,
            ref CharacterAnimationComponent playerAnimationComponent,
            ref StunComponent playerStunComponent,
            ref MovementInputComponent move, ref JumpInputComponent jump,
            CharacterController playerCharacter, IMultiplayerMovementSettings settings);
    }
}
