using DCL.CharacterMotion.Components;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Settings
{
    public abstract class SendRuleBase : ScriptableObject
    {
        public Color color = Color.white;
        public float MinTimeDelta = 0;
        public virtual string Message { get; set; }

        public abstract bool IsSendConditionMet(
            in float t,
            in NetworkMovementMessage lastNetworkMovementMessage,
            in CharacterAnimationComponent playerAnimationComponent,
            in StunComponent playerStunComponent,
            in MovementInputComponent move,
            in JumpInputComponent jump,
            CharacterController playerCharacter,
            IMultiplayerMovementSettings settings);
    }
}
