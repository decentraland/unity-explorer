using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.MainPlayerTriggerArea
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PLAYER_TRIGGER_AREA)]
    [ThrottlingEnabled]
    public partial class MainPlayerTriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<MainPlayerTriggerArea> poolRegistry;
        private readonly MainPlayerAvatarBase mainPlayerAvatarBase;
        private readonly ISceneStateProvider sceneStateProvider;

        public MainPlayerTriggerAreaHandlerSystem(World world, IComponentPool<MainPlayerTriggerArea> poolRegistry, MainPlayerAvatarBase mainPlayerAvatarBase, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.mainPlayerAvatarBase = mainPlayerAvatarBase;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBase.Configured) return;

            UpdateMainPlayerTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateMainPlayerTriggerArea(ref TransformComponent transformComponent, ref MainPlayerTriggerAreaComponent mainPlayerTriggerAreaComponent)
        {
            MainPlayerTriggerArea triggerAreaMonoBehaviour = mainPlayerTriggerAreaComponent.MonoBehaviour;

            if (!sceneStateProvider.IsCurrent)
            {
                if (triggerAreaMonoBehaviour != null)
                {
                    // triggerAreaMonoBehaviour.OnExitedTrigger?.Invoke();
                    triggerAreaMonoBehaviour.boxCollider.enabled = false;
                }

                return;
            }

            if (mainPlayerTriggerAreaComponent.IsDirty)
            {
                mainPlayerTriggerAreaComponent.IsDirty = false;

                if (triggerAreaMonoBehaviour == null)
                {
                    triggerAreaMonoBehaviour = poolRegistry.Get();
                    mainPlayerTriggerAreaComponent.MonoBehaviour = triggerAreaMonoBehaviour;
                }

                // Values that may have been updated from the scene
                triggerAreaMonoBehaviour.ClearEvents();
                triggerAreaMonoBehaviour.OnEnteredTrigger += mainPlayerTriggerAreaComponent.OnEnteredTrigger;
                triggerAreaMonoBehaviour.OnExitedTrigger += mainPlayerTriggerAreaComponent.OnExitedTrigger;
                mainPlayerTriggerAreaComponent.MonoBehaviour.boxCollider.size = mainPlayerTriggerAreaComponent.areaSize;
            }

            Transform triggerAreaTransform = triggerAreaMonoBehaviour.transform;

            if (transformComponent.Cached.WorldPosition != triggerAreaTransform.position)
                triggerAreaTransform.position = transformComponent.Cached.WorldPosition;

            if (transformComponent.Cached.WorldRotation != triggerAreaTransform.rotation)
                triggerAreaTransform.rotation = transformComponent.Cached.WorldRotation;

            if (!triggerAreaMonoBehaviour.boxCollider.enabled)
                triggerAreaMonoBehaviour.boxCollider.enabled = true; // TODO: Disable when returning to pool...
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
