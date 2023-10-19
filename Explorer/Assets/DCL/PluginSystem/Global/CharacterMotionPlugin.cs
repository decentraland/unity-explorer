using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
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

        public async UniTask Initialize(CharacterMotionSettings settings, CancellationToken ct)
        {
            this.settings = await assetsProvisioner.ProvideMainAsset(settings.controllerSettings, ct);
        }

        public void Dispose()
        {
            settings.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            Arch.Core.World world = builder.World;

            // Add Motion components
            world.Add(arguments.PlayerEntity,
                new CharacterRigidTransform(),
                (ICharacterControllerSettings)settings.Value,
                characterObject.Controller);

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder);
        }
    }
}
