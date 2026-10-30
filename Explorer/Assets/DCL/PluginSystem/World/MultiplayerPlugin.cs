using Arch.Core;
using Arch.SystemGroups;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;
using WriteAvatarEmoteCommandSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEmoteCommandSystem;
using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;
using WritePlayerIdentityDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WritePlayerIdentityDataSystem;
using WriteSDKAvatarBaseSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteSDKAvatarBaseSystem;

namespace DCL.PluginSystem.World
{
    public class MultiplayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly Arch.Core.World globalWorld;
        private readonly Entity localPlayerEntity;
        private readonly CharacterDataPropagationUtility characterDataPropagationUtility;

        public MultiplayerPlugin(
            Arch.Core.World globalWorld,
            Entity localPlayerEntity,
            CharacterDataPropagationUtility characterDataPropagationUtility)
        {
            this.globalWorld = globalWorld;
            this.localPlayerEntity = localPlayerEntity;
            this.characterDataPropagationUtility = characterDataPropagationUtility;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in SystemsDependencies systemsDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LocalPlayerCRDTEntityHandlerSystem.InjectToWorld(ref builder, globalWorld, localPlayerEntity, characterDataPropagationUtility, persistentEntities);

            WritePlayerIdentityDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteSDKAvatarBaseSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEquippedDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEmoteCommandSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);
            WritePlayerTransformSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneData);

            CleanUpAvatarPropagationComponentsSystem.InjectToWorld(ref builder);
        }
    }
}
