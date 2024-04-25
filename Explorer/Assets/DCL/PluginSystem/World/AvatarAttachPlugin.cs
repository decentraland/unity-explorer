using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarAttach.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AvatarAttachPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AvatarAttachPlugin(ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InstantiateTransformForAvatarAttachSystem.InjectToWorld(ref builder, componentPoolsRegistry.GetReferenceTypePool<Transform>(), persistentEntities.SceneRoot);

            ResetDirtyFlagSystem<PBAvatarAttach>.InjectToWorld(ref builder);
            var avatarShapeHandlerSystem = AvatarAttachHandlerSystem.InjectToWorld(ref builder, mainPlayerAvatarBaseProxy, sharedDependencies.SceneStateProvider);
            finalizeWorldSystems.Add(avatarShapeHandlerSystem);
        }
    }
}
