using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.SDKComponents.AvatarShape.Systems;
using ECS.LifeCycle;
using ECS.LifeCycle.Systems;
using ECS.Unity.AvatarShape.Systems;
using Global.Dynamic.LaunchModes;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PluginSystem.World
{
    public class AvatarShapePlugin : IDCLWorldPlugin
    {
        private readonly Arch.Core.World globalWorld;
        private readonly IComponentPool<Transform> globalTransformPool;
        private readonly ILaunchMode launchMode;

        public AvatarShapePlugin(Arch.Core.World globalWorld, IComponentPoolsRegistry poolRegistry, ILaunchMode launchMode)
        {
            this.globalWorld = globalWorld;
            this.launchMode = launchMode;
            this.globalTransformPool = poolRegistry.GetReferenceTypePool<Transform>();
        }

        public void Dispose()
        {
            //ignore
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            ResetDirtyFlagSystem<PBAvatarShape>.InjectToWorld(ref builder);
            finalizeWorldSystems.Add(AvatarShapeHandlerSystem.InjectToWorld(ref builder, globalWorld, globalTransformPool, sharedDependencies.SceneData, launchMode.CurrentMode == LaunchMode.LocalSceneDevelopment));
            UpdateAvatarShapeInterpolateMovementSystem.InjectToWorld(ref builder, globalWorld, sharedDependencies.SceneData.SceneShortInfo.BaseParcel);
        }
    }
}
