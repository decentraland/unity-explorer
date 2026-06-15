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

            // Each flag gates only its own action so they can be disabled independently.
            // The jump button is shared between normal jump, air/double jump and gliding; which action a
            // press maps to is decided downstream from JumpCount vs MaxAirJumpCount in ApplyJump/ApplyGliding.

            bool isNormalJump = jumpState.JumpCount == 0;

            bool disableJump = inputModifierComponent.DisableJump;
            bool disableDoubleJump = inputModifierComponent.DisableDoubleJump || !FeaturesRegistry.Instance.IsEnabled(FeatureId.DOUBLE_JUMP);
            bool disableGliding = inputModifierComponent.DisableGliding || !FeaturesRegistry.Instance.IsEnabled(FeatureId.GLIDING);

            if (disableJump && isNormalJump)
                // Trying to do a normal (ground) jump but jumping is disabled.
                // Gliding is impossible from a normal jump state (JumpCount == 0), so this never blocks a glide.
                return;

            if (disableDoubleJump && disableGliding && !isNormalJump)
                // Trying to air jump but BOTH double jump and gliding are disabled.
                // If gliding is still enabled we let the press through so the player can glide while falling.
                return;

            // Double jump availability depends solely on disableDoubleJump.
            // Setting 0 max air jumps routes the input straight to gliding.
            jumpState.MaxAirJumpCount = disableDoubleJump ? 0 : settings.AirJumpCount;

            if (disableGliding && jumpState.JumpCount > jumpState.MaxAirJumpCount)
                // Trying to glide but gliding is disabled
                return;

            // Flags allow jump input to pass through

            if (inputAction.WasPressedThisFrame()) inputToUpdate.Trigger.TickWhenJumpOccurred = tickValue + 1;
            inputToUpdate.IsPressed = inputAction.IsPressed();
        }
    }
}
