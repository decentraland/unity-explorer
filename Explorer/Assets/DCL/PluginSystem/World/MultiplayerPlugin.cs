using Arch.SystemGroups;
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
        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            WritePlayerIdentityDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteSDKAvatarBaseSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEquippedDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEmoteCommandSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);
            WritePlayerTransformSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneData);

            CleanUpAvatarPropagationComponentsSystem.InjectToWorld(ref builder);
        }
    }
}
