using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Settings;
using DCL.SDKComponents.AvatarLocomotion.Components;
using ECS.Abstract;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class SetupAvatarLocomotionOverridesSystem : BaseUnityLoopSystem
    {
        public SetupAvatarLocomotionOverridesSystem(World world) : base(world)
        {
        }

        protected override void Update(float t) =>
            SetupEntityQuery(World);

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(AvatarLocomotionOverrides))]
        private void SetupEntity(Entity entity, in ICharacterControllerSettings controllerSettingsImpl)
        {
            World.Add<AvatarLocomotionOverrides>(entity);

            // Replace the interface type with the specific overridable implementation
            var overridableSettings = new OverridableCharacterControllerSettings(controllerSettingsImpl);
            World.Set<ICharacterControllerSettings>(entity, overridableSettings);

            // Add with the specific type to filter in queries
            World.Add(entity, overridableSettings);
        }
    }
}
