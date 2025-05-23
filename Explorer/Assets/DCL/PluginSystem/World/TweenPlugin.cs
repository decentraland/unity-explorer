using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.Tween.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using DCL.SDKComponents.Tween.Components;
using DCL.SDKComponents.Tween.Playground;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class TweenPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly TweenerPool tweenerPool;
        private readonly INtpTimeService ntpClient;
        private readonly IEntityCollidersGlobalCache collidersGlobalCache;
        private readonly Arch.Core.World globalWorld;

        public TweenPlugin(Arch.Core.World globalWorld, INtpTimeService ntpClient, IEntityCollidersGlobalCache collidersGlobalCache)
        {
            this.globalWorld = globalWorld;
            this.ntpClient = ntpClient;
            this.collidersGlobalCache = collidersGlobalCache;
            tweenerPool = new TweenerPool();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBTween>.InjectToWorld(ref builder);
            TweenLoaderSystem.InjectToWorld(ref builder);

            TweenUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool, sharedDependencies.SceneStateProvider, ntpClient, collidersGlobalCache, globalWorld);
            WriteNtpTimeSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, ntpClient);

            finalizeWorldSystems.Add(TweenCleanUpSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool));
        }
    }
}
