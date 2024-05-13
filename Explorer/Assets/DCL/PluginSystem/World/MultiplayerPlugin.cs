using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.SDK.Components;
using DCL.PluginSystem.World.Dependencies;
using DCL.Profiles;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;
using WriteAvatarEmoteCommandSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEmoteCommandSystem;
using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;
using WritePlayerIdentityDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WritePlayerIdentityDataSystem;
using WriteSDKAvatarBaseSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteSDKAvatarBaseSystem;

namespace DCL.PluginSystem.World
{
    public class MultiplayerPlugin : IDCLWorldPluginWithoutSettings
    {
        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<Profile>.InjectToWorld(ref builder);
            ResetDirtyFlagSystem<AvatarEmoteCommandComponent>.InjectToWorld(ref builder);

            WritePlayerIdentityDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteSDKAvatarBaseSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEquippedDataSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter);
            WriteAvatarEmoteCommandSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, sharedDependencies.SceneStateProvider);
        }
    }
}
