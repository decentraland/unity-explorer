using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarAttach.Systems;
using DCL.Utilities;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AvatarAttachPlugin : IDCLWorldPlugin
    {
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly Arch.Core.World globalWorld;

        public AvatarAttachPlugin(Arch.Core.World globalWorld,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.globalWorld = globalWorld;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            InstantiateTransformForAvatarAttachSystem.InjectToWorld(ref builder, componentPoolsRegistry.GetReferenceTypePool<Transform>(), persistentEntities.SceneRoot);

            ResetDirtyFlagSystem<PBAvatarAttach>.InjectToWorld(ref builder);

            var avatarShapeHandlerSystem = AvatarAttachHandlerSystem.InjectToWorld(ref builder,
                globalWorld,
                mainPlayerAvatarBaseProxy,
                sharedDependencies.SceneStateProvider);

            finalizeWorldSystems.Add(avatarShapeHandlerSystem);

            AvatarAttachHandlerSetupSystem.InjectToWorld(ref builder,
                globalWorld,
                mainPlayerAvatarBaseProxy,
                sharedDependencies.SceneStateProvider);
        }
    }
}
