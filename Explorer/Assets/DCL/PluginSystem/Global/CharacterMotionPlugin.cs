using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.Character;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Systems;
using DCL.DebugUtilities;
using DCL.Multiplayer.Emotes;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class CharacterMotionPlugin : IDCLGlobalPlugin<CharacterMotionSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ICharacterObject characterObject;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IEmoteCache emoteCache;
        private readonly MultiplayerEmotesMessageBus multiplayerEmotesMessageBus;

        private ProvidedAsset<CharacterControllerSettings> settings;

        public CharacterMotionPlugin(IAssetsProvisioner assetsProvisioner,
            ICharacterObject characterObject,
            IDebugContainerBuilder debugContainerBuilder,
            IEmoteCache emoteCache, MultiplayerEmotesMessageBus multiplayerEmotesMessageBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.characterObject = characterObject;
            this.debugContainerBuilder = debugContainerBuilder;
            this.emoteCache = emoteCache;
            this.multiplayerEmotesMessageBus = multiplayerEmotesMessageBus;
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
                new CharacterAnimationComponent(),
                new CharacterEmoteComponent(),
                new CharacterPlatformComponent(),
                new StunComponent(),
                new FeetIKComponent(),
                new HandsIKComponent(),
                new HeadIKComponent());

            InterpolateCharacterSystem.InjectToWorld(ref builder);
            RotateCharacterSystem.InjectToWorld(ref builder);
            CalculateCharacterVelocitySystem.InjectToWorld(ref builder, debugContainerBuilder);
            CharacterAnimationSystem.InjectToWorld(ref builder);
            CharacterPlatformSystem.InjectToWorld(ref builder);
            StunCharacterSystem.InjectToWorld(ref builder);
            CalculateCameraFovSystem.InjectToWorld(ref builder);
            FeetIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HandsIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
            HeadIKSystem.InjectToWorld(ref builder, debugContainerBuilder);
        }
    }
}
