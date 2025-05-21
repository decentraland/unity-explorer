using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.PrimaryPointerInfo.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class PrimaryPointerInfoPlugin : IDCLWorldPlugin<NoExposedPluginSettings>
    {
        private readonly Arch.Core.World globalWorld;
        private readonly ExposedCameraData exposedCameraData;

        public PrimaryPointerInfoPlugin(Arch.Core.World globalWorld, ExposedCameraData exposedCameraData)
        {
            this.globalWorld = globalWorld;
            this.exposedCameraData = exposedCameraData;
        }

        public void Dispose()
        {
            // No cleanup needed
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            PrimaryPointerInfoSystem.InjectToWorld(
                ref builder,
                globalWorld,
                sharedDependencies.EcsToCRDTWriter,
                exposedCameraData
            );

            ResetDirtyFlagSystem<PBPrimaryPointerInfo>.InjectToWorld(ref builder);
        }

        public UniTask InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
