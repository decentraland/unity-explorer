using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class PlayerSpatialStateNetSendSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerSpatialStateSettings settings;

        private readonly CharacterController playerCharacter;
        private readonly CharacterAnimationComponent playerAnimationComponent;
        private readonly StunComponent playerStunComponent;

        public PlayerSpatialStateNetSendSystem(World world, IMultiplayerSpatialStateSettings settings, CharacterController playerCharacter,
            CharacterAnimationComponent playerAnimationComponent, StunComponent playerStunComponent) : base(world)
        {
            this.settings = settings;
            this.playerCharacter = playerCharacter;
            this.playerAnimationComponent = playerAnimationComponent;
            this.playerStunComponent = playerStunComponent;
        }

        protected override void Update(float t)
        {
            var message = new MessageMock
            {
                timestamp = UnityEngine.Time.unscaledTime,
                position = playerCharacter.transform.position,
                velocity = playerCharacter.velocity,
                animState = playerAnimationComponent.States,
                isStunned = playerStunComponent.IsStunned,
            };

            Debug.Log(message.position);

        }
    }
}
