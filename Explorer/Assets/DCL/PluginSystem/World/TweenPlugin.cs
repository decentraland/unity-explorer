using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using DCL.SDKComponents.Tween.Components;
using TweenCleanUpSystem = DCL.SDKComponents.Tween.TweenCleanUpSystem;
using TweenLoaderSystem = DCL.SDKComponents.Tween.TweenLoaderSystem;
using TweenUpdaterSystem = DCL.SDKComponents.Tween.TweenUpdaterSystem;
using TweenSequenceUpdaterSystem = DCL.SDKComponents.Tween.TweenSequenceUpdaterSystem;

namespace DCL.PluginSystem.World
{
    public class TweenPlugin : IDCLWorldPluginWithoutSettings
    {
        // Previous SDK versions have the TweenSequence logic running in the SDK Runtime
        // and we cannot have both running at the same time (SDK Runtime & Explorer)
        private const string MIN_TWEEN_SEQUENCE_SDK_VERSION = "7.11.1"; // TODO: DEFINE CLOSE TO THE RELEASE...

        private readonly TweenerPool tweenerPool;

        public TweenPlugin()
        {
            tweenerPool = new TweenerPool();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBTween>.InjectToWorld(ref builder);
            TweenLoaderSystem.InjectToWorld(ref builder);
            TweenUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool, sharedDependencies.SceneStateProvider);

            if (sharedDependencies.SceneData.IsSDKVersionOrHigher(MIN_TWEEN_SEQUENCE_SDK_VERSION))
            {
                ResetDirtyFlagSystem<PBTweenSequence>.InjectToWorld(ref builder);
                TweenSequenceUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool, sharedDependencies.SceneStateProvider);
            }

            finalizeWorldSystems.Add(TweenCleanUpSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool));
        }
    }
}
