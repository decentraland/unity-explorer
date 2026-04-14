using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.FeatureFlags;
using DCL.Input;
using DCL.Input.Systems;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using UnityEngine.InputSystem;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateInputJumpSystem : UpdateInputSystem<JumpInputComponent, PlayerComponent>
    {
        private readonly InputAction inputAction;

        private SingleInstanceEntity fixedTick;

        public UpdateInputJumpSystem(World world, InputAction inputAction) : base(world)
        {
            this.inputAction = inputAction;
        }

        public override void Initialize()
        {
            base.Initialize();

            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t) =>
            UpdateInputQuery(World, fixedTick.GetPhysicsTickComponent(World).Tick);

        [Query]
        private void UpdateInput([Data] int tickValue, ref JumpInputComponent inputToUpdate, ref JumpState jumpState, in InputModifierComponent inputModifierComponent, in ICharacterControllerSettings settings)
        {
            if (!inputAction.enabled)
                // The input action itself is disabled, no jumping or gliding are allowed
                return;

            // Jump and double jump only prevent input if gliding is also disabled
            // That way it's still possible to glide when falling down, even if jumping is not possible

            bool isNormalJump = jumpState.JumpCount == 0;

            bool disableJump = inputModifierComponent.DisableJump;
            bool disableDoubleJump = inputModifierComponent.DisableDoubleJump || !FeaturesRegistry.Instance.IsEnabled(FeatureId.DOUBLE_JUMP);
            bool disableGliding = inputModifierComponent.DisableGliding || !FeaturesRegistry.Instance.IsEnabled(FeatureId.GLIDING);

            if (disableJump && disableGliding && isNormalJump)
                // Trying to jump but BOTH normal jump and gliding are disabled
                return;

            if (disableDoubleJump && disableGliding && !isNormalJump)
                // Trying to double jump but BOTH double jump and gliding are disabled
                return;

            // If jumping is disabled set 0 max air jumps regardless of settings, that way we go directly to gliding
            jumpState.MaxAirJumpCount = disableJump || disableDoubleJump ? 0 : settings.AirJumpCount;

            if (disableGliding && jumpState.JumpCount > jumpState.MaxAirJumpCount)
                // Trying to glide but gliding is disabled
                return;

            // Flags allow jump input to pass through

            if (inputAction.WasPressedThisFrame()) inputToUpdate.Trigger.TickWhenJumpOccurred = tickValue + 1;
            inputToUpdate.IsPressed = inputAction.IsPressed();
        }
    }
}
