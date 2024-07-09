using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.Tween.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;
using DCL.SDKComponents.Tween.Components;
using UnityEngine.Pool;

namespace DCL.PluginSystem.World
{
    public class TweenPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly TweenerPool tweenerPool;
        private readonly IObjectPool<PBTween> pbTweenObjectPool;

        public TweenPlugin()
        {
            pbTweenObjectPool = new ObjectPool<PBTween>(() => new PBTween());
            tweenerPool = new TweenerPool();
        }
        
        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBTween>.InjectToWorld(ref builder);
            TweenLoaderSystem.InjectToWorld(ref builder, pbTweenObjectPool);
            var tweenUpdaterSystem = TweenUpdaterSystem.InjectToWorld(ref builder, sharedDependencies.EcsToCRDTWriter, tweenerPool);
            finalizeWorldSystems.Add(tweenUpdaterSystem);
        }
    }
}
