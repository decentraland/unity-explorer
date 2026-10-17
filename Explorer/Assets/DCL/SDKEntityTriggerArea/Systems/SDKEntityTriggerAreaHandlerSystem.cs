using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKEntityTriggerArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class SDKEntityTriggerAreaHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly IComponentPool<SDKEntityTriggerArea> poolRegistry;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly Transform mainPlayerTransform;
        private readonly ISceneStateProvider sceneStateProvider;

        public SDKEntityTriggerAreaHandlerSystem(World world, IComponentPool<SDKEntityTriggerArea> poolRegistry, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, ISceneStateProvider sceneStateProvider, ICharacterObject characterObject) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
            mainPlayerTransform = characterObject.Transform;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent || !mainPlayerAvatarBaseProxy.Configured) return;

            UpdateSDKEntityTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateSDKEntityTriggerArea(ref TransformComponent transformComponent, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (!triggerAreaComponent.IsDirty)
            {
                triggerAreaComponent.TryUpdateParentScaleCompensation(transformComponent.Transform);
                return;
            }

            triggerAreaComponent.IsDirty = false;

            triggerAreaComponent.TryAssignArea(poolRegistry, mainPlayerTransform, transformComponent);
        }

        [Query]
        private void UpdateSDKEntityTriggerAreaOnSceneChange([Data] bool isCurrentScene, ref TransformComponent transformComponent, ref SDKEntityTriggerAreaComponent triggerAreaComponent)
        {
            if (!isCurrentScene)
            {
                triggerAreaComponent.TryDispose();
                return;
            }

            triggerAreaComponent.TryAssignArea(poolRegistry, mainPlayerTransform, transformComponent);
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            if(sceneStateProvider.State.Value() == SceneState.Disposed) return;

            UpdateSDKEntityTriggerAreaOnSceneChangeQuery(World, value);
        }
    }
}
