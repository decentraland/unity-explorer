using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion;
using DCL.CharacterMotion.Settings;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
using UnityEngine.UIElements;

namespace DCL.Character.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(CalculateCharacterVelocitySystem))]
    public partial class CharacterVelocityDebugSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity settingsEntity;

        private readonly ElementBinding<float> cameraRunFov;
        private readonly ElementBinding<float> walkSpeed;
        private readonly ElementBinding<float> jogSpeed;
        private readonly ElementBinding<float> runSpeed;
        private readonly ElementBinding<float> jogJumpHeight;
        private readonly ElementBinding<float> runJumpHeight;
        private readonly ElementBinding<float> jumpHold;
        private readonly ElementBinding<float> jumpHoldGravity;
        private readonly ElementBinding<float> gravity;
        private readonly ElementBinding<float> airAcc;
        private readonly ElementBinding<float> maxAirAcc;
        private readonly ElementBinding<float> airDrag;
        private readonly ElementBinding<float> stopTime;

        private readonly ElementBinding<float> characterMass;
        private readonly ElementBinding<float> externalEnvDrag;
        private readonly ElementBinding<float> externalGroundFriction;
        private readonly ElementBinding<float> maxExternalVelocity;

        private readonly ElementBinding<int> airJumpCount = new (0);
        private readonly ElementBinding<float> airJumpHeight = new (0);
        private readonly ElementBinding<float> airJumpDelay = new (0);
        private readonly ElementBinding<float> airJumpGravityDuringDelay = new (0);
        private readonly ElementBinding<float> cooldownBetweenJumps = new (0);
        private readonly ElementBinding<float> airJumpImpulse = new (0);

        internal CharacterVelocityDebugSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            cameraRunFov = new ElementBinding<float>(0, OnDebugValueChanged);
            walkSpeed = new ElementBinding<float>(0, OnDebugValueChanged);
            jogSpeed = new ElementBinding<float>(0, OnDebugValueChanged);
            runSpeed = new ElementBinding<float>(0, OnDebugValueChanged);
            jogJumpHeight = new ElementBinding<float>(0, OnDebugValueChanged);
            runJumpHeight = new ElementBinding<float>(0, OnDebugValueChanged);
            jumpHold = new ElementBinding<float>(0, OnDebugValueChanged);
            jumpHoldGravity = new ElementBinding<float>(0, OnDebugValueChanged);
            gravity = new ElementBinding<float>(0, OnDebugValueChanged);
            airAcc = new ElementBinding<float>(0, OnDebugValueChanged);
            maxAirAcc = new ElementBinding<float>(0, OnDebugValueChanged);
            airDrag = new ElementBinding<float>(0, OnDebugValueChanged);
            stopTime = new ElementBinding<float>(0, OnDebugValueChanged);
            characterMass = new ElementBinding<float>(0, OnDebugValueChanged);
            externalEnvDrag = new ElementBinding<float>(0, OnDebugValueChanged);
            externalGroundFriction = new ElementBinding<float>(0, OnDebugValueChanged);
            maxExternalVelocity = new ElementBinding<float>(0, OnDebugValueChanged);

            debugBuilder.TryAddWidget("Locomotion: Base")
                       ?.AddFloatField("Camera Run FOV", cameraRunFov)
                        .AddFloatField("Walk Speed", walkSpeed)
                        .AddFloatField("Jog Speed", jogSpeed)
                        .AddFloatField("Run Speed", runSpeed)
                        .AddFloatField("Jog Jump Height", jogJumpHeight)
                        .AddFloatField("Run Jump Height", runJumpHeight)
                        .AddFloatField("Jump Hold Time", jumpHold)
                        .AddFloatField("Jump Hold Gravity Scale", jumpHoldGravity)
                        .AddFloatField("Gravity", gravity)
                        .AddFloatField("Air Acceleration", airAcc)
                        .AddFloatField("Max Air Acceleration", maxAirAcc)
                        .AddFloatField("Air Drag", airDrag)
                        .AddFloatField("Grounded Stop Time", stopTime);

            debugBuilder.TryAddWidget("Locomotion: External Forces")?
                        .AddFloatField("Character Mass", characterMass)
                        .AddFloatField("External Env Drag", externalEnvDrag)
                        .AddFloatField("External Ground Friction", externalGroundFriction)
                        .AddFloatField("Max External Velocity", maxExternalVelocity);

            debugBuilder.TryAddWidget("Locomotion: Air Jumping")?
                        .AddControl( new DebugConstLabelDef("Air Jump Count"), new DebugIntFieldDef(airJumpCount) )
                        .AddFloatField("Height", airJumpHeight)
                        .AddFloatField("Delay", airJumpDelay)
                        .AddFloatField("Gravity during Delay", airJumpGravityDuringDelay)
                        .AddFloatField("Cooldown", cooldownBetweenJumps)
                        .AddFloatField("Direction Change Impulse", airJumpImpulse);
        }

        public override void Initialize()
        {
            settingsEntity = World.CacheCharacterSettings();
            SyncBindingsFromSettings(settingsEntity.GetCharacterSettings(World));
        }

        protected override void Update(float t)
        {
            SyncBindingsFromSettings(settingsEntity.GetCharacterSettings(World));
        }

        private void OnDebugValueChanged(ChangeEvent<float> _)
        {
            if (((Entity)settingsEntity).IsNull())
                return;

            ApplyDebugBindingsToSettings(settingsEntity.GetCharacterSettings(World));
        }

        private void SyncBindingsFromSettings(ICharacterControllerSettings settings)
        {
            // Base
            cameraRunFov.Value = settings.CameraFOVWhileRunning;
            walkSpeed.Value = settings.WalkSpeed;
            jogSpeed.Value = settings.JogSpeed;
            runSpeed.Value = settings.RunSpeed;
            jogJumpHeight.Value = settings.JogJumpHeight;
            runJumpHeight.Value = settings.RunJumpHeight;
            jumpHold.Value = settings.LongJumpTime;
            jumpHoldGravity.Value = settings.LongJumpGravityScale;
            gravity.Value = settings.Gravity;
            airAcc.Value = settings.AirAcceleration;
            maxAirAcc.Value = settings.MaxAirAcceleration;
            airDrag.Value = settings.AirDrag;
            stopTime.Value = settings.StopTimeSec;

            // External Force/Impulse
            characterMass.Value = settings.CharacterMass;
            externalEnvDrag.Value = settings.ExternalEnvDrag;
            externalGroundFriction.Value = settings.ExternalGroundFriction;
            maxExternalVelocity.Value = settings.MaxExternalVelocity;

            // Glide and doubleJump
            airJumpCount.Value = settings.AirJumpCount;
            airJumpHeight.Value = settings.AirJumpHeight;
            airJumpDelay.Value = settings.AirJumpDelay;
            airJumpGravityDuringDelay.Value = settings.AirJumpGravityDuringDelay;
            cooldownBetweenJumps.Value = settings.CooldownBetweenJumps;
            airJumpImpulse.Value = settings.AirJumpDirectionChangeImpulse;
        }

        private void ApplyDebugBindingsToSettings(ICharacterControllerSettings settings)
        {
            // Base
            settings.CameraFOVWhileRunning = cameraRunFov.Value;
            settings.WalkSpeed = walkSpeed.Value;
            settings.JogSpeed = jogSpeed.Value;
            settings.RunSpeed = runSpeed.Value;
            settings.JogJumpHeight = jogJumpHeight.Value;
            settings.RunJumpHeight = runJumpHeight.Value;
            settings.LongJumpTime = jumpHold.Value;
            settings.LongJumpGravityScale = jumpHoldGravity.Value;
            settings.Gravity = gravity.Value;
            settings.AirAcceleration = airAcc.Value;
            settings.MaxAirAcceleration = maxAirAcc.Value;
            settings.AirDrag = airDrag.Value;
            settings.StopTimeSec = stopTime.Value;

            // External Force/Impulse
            settings.CharacterMass = characterMass.Value;
            settings.ExternalEnvDrag = externalEnvDrag.Value;
            settings.ExternalGroundFriction = externalGroundFriction.Value;
            settings.MaxExternalVelocity = maxExternalVelocity.Value;

            // Glide and doubleJump
            settings.AirJumpCount = airJumpCount.Value;
            settings.AirJumpHeight = airJumpHeight.Value;
            settings.AirJumpDelay = airJumpDelay.Value;
            settings.AirJumpGravityDuringDelay = airJumpGravityDuringDelay.Value;
            settings.CooldownBetweenJumps = cooldownBetweenJumps.Value;
            settings.AirJumpDirectionChangeImpulse = airJumpImpulse.Value;
        }
    }
}
