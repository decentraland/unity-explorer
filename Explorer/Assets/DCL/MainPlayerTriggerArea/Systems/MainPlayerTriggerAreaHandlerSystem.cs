using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
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

        public MainPlayerTriggerAreaHandlerSystem(World world, IComponentPool<MainPlayerTriggerArea> poolRegistry) : base(world)
        {
            this.poolRegistry = poolRegistry;
        }

        protected override void Update(float t)
        {
            UpdateMainPlayerTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateMainPlayerTriggerArea(ref TransformComponent transformComponent, ref MainPlayerTriggerAreaComponent mainPlayerTriggerAreaComponent)
        {
            if (!mainPlayerTriggerAreaComponent.IsDirty) return;

            if (mainPlayerTriggerAreaComponent.MonoBehaviour == null)
            {
                MainPlayerTriggerArea triggerArea = poolRegistry.Get();

                // TODO: Support changing actions as well?
                triggerArea.OnEnteredTrigger += mainPlayerTriggerAreaComponent.OnEnteredTrigger;
                triggerArea.OnExitedTrigger += mainPlayerTriggerAreaComponent.OnExitedTrigger;

                Transform transform = triggerArea.transform;
                transform.SetParent(transformComponent.Transform);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;

                triggerArea.boxCollider.enabled = true; // TODO: Disable when returning to pool...
                mainPlayerTriggerAreaComponent.MonoBehaviour = triggerArea;
            }

            mainPlayerTriggerAreaComponent.MonoBehaviour.boxCollider.size = mainPlayerTriggerAreaComponent.areaSize;
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
