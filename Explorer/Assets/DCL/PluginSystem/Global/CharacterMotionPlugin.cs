using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.Time.Systems;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class CharacterMotionPlugin : IDCLGlobalPlugin<CharacterMotionSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICharacterObject characterObject;

        private ProvidedAsset<CharacterControllerSettings> settings;

        public CharacterMotionPlugin(IAssetsProvisioner assetsProvisioner, ICharacterObject characterObject)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.characterObject = characterObject;
        }

        public void Dispose()
        {
            settings.Dispose();
        }

        public async UniTask InitializeAsync(CharacterMotionSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAssetAsync(settings.controllerSettings, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                (ICharacterControllerSettings)settings.Value,
                characterObject.Controller,
                new CharacterAnimationComponent(new CharacterAnimationComponent.AnimationTriggers()),
                new CharacterPlatformComponent());

            // TODO: Move these elsewhere? They should be global-er
            UpdatePhysicsTickSystem.InjectToWorld(ref builder);
            UpdateTimeSystem.InjectToWorld(ref builder);

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder);
            CharacterAnimationSystem.InjectToWorld(ref builder);
            CharacterPlatformSystem.InjectToWorld(ref builder);
        }
    }
}
