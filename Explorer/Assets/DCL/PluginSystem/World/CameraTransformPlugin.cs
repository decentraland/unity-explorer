using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.CameraTransform.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class CameraTransformPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly ObjectProxy<Arch.Core.World> globalWorldProxy;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public CameraTransformPlugin(ObjectProxy<Entity> cameraEntityProxy, ObjectProxy<Arch.Core.World> globalWorldProxy, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.cameraEntityProxy = cameraEntityProxy;
            this.globalWorldProxy = globalWorldProxy;
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public void Dispose()
        {
            //ignore
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            finalizeWorldSystems.Add(
                SDKCameraTransformSystem.InjectToWorld(
                    ref builder,
                    sharedDependencies.EntitiesMap,
                    cameraEntityProxy,
                    globalWorldProxy,
                    componentPoolsRegistry.GetReferenceTypePool<Transform>())
            );
        }
    }
}
