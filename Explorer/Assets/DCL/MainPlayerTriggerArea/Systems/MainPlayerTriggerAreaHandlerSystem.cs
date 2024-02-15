using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Transform;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
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

        public MainPlayerTriggerAreaHandlerSystem(World world, IComponentPool<MainPlayerTriggerArea> poolRegistry, MainPlayerAvatarBase mainPlayerAvatarBase) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.mainPlayerAvatarBase = mainPlayerAvatarBase;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBase.Configured) return;

            // TODO: Handle 'current scene' with sceneProvider

            UpdateMainPlayerTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateMainPlayerTriggerArea(ref TransformComponent transformComponent, ref MainPlayerTriggerAreaComponent mainPlayerTriggerAreaComponent)
        {
            if (mainPlayerTriggerAreaComponent.IsDirty)
            {
                mainPlayerTriggerAreaComponent.IsDirty = false;

                if (mainPlayerTriggerAreaComponent.MonoBehaviour == null)
                {
                    MainPlayerTriggerArea triggerArea = poolRegistry.Get();

                    // TODO: Find a way of enparenting to scene GAMEOBJECT
                    // triggerArea.transform.SetParent();

                    triggerArea.OnEnteredTrigger += mainPlayerTriggerAreaComponent.OnEnteredTrigger;
                    triggerArea.OnExitedTrigger += mainPlayerTriggerAreaComponent.OnExitedTrigger;
                    triggerArea.boxCollider.enabled = true; // TODO: Disable when returning to pool...
                    mainPlayerTriggerAreaComponent.MonoBehaviour = triggerArea;
                }

                // TODO: Support changing actions as well?
                mainPlayerTriggerAreaComponent.MonoBehaviour.boxCollider.size = mainPlayerTriggerAreaComponent.areaSize;
            }

            // TODO: Optimize here similar to AvatarAttachHandlerSystem.ApplyAnchorPointTransformValues()...
            Transform triggerAreaTransform = mainPlayerTriggerAreaComponent.MonoBehaviour.transform;
            triggerAreaTransform.position = transformComponent.Cached.WorldPosition;
            triggerAreaTransform.rotation = transformComponent.Cached.WorldRotation;
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
