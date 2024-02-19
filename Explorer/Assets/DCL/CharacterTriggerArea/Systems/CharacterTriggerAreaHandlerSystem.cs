using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    [ThrottlingEnabled]
    public partial class CharacterTriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CharacterTriggerArea> poolRegistry;
        private readonly MainPlayerReferences mainPlayerReferences;
        private readonly ISceneStateProvider sceneStateProvider;

        public CharacterTriggerAreaHandlerSystem(World world, IComponentPool<CharacterTriggerArea> poolRegistry, MainPlayerReferences mainPlayerReferences, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.mainPlayerReferences = mainPlayerReferences;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerReferences.MainPlayerAvatarBase.Configured) return;

            UpdateCharacterTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateCharacterTriggerArea(ref TransformComponent transformComponent, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            CharacterTriggerArea triggerAreaMonoBehaviour = characterTriggerAreaComponent.MonoBehaviour;

            if (!sceneStateProvider.IsCurrent)
            {
                if (triggerAreaMonoBehaviour != null)
                {
                    triggerAreaMonoBehaviour.BoxCollider.enabled = false;
                    triggerAreaMonoBehaviour.ForceOnTriggerExit();
                }

                return;
            }

            if (characterTriggerAreaComponent.IsDirty)
            {
                characterTriggerAreaComponent.IsDirty = false;

                if (triggerAreaMonoBehaviour == null)
                {
                    triggerAreaMonoBehaviour = poolRegistry.Get();
                    characterTriggerAreaComponent.MonoBehaviour = triggerAreaMonoBehaviour;

                    if (characterTriggerAreaComponent.targetOnlyMainPlayer)
                        triggerAreaMonoBehaviour.TargetTransform = mainPlayerReferences.MainPlayerTransform.Transform;
                }

                // Values that may have been updated from the SDK scene
                triggerAreaMonoBehaviour.ClearEvents();
                triggerAreaMonoBehaviour.OnEnteredTrigger += characterTriggerAreaComponent.OnEnteredTrigger;
                triggerAreaMonoBehaviour.OnExitedTrigger += characterTriggerAreaComponent.OnExitedTrigger;
                characterTriggerAreaComponent.MonoBehaviour.BoxCollider.size = characterTriggerAreaComponent.areaSize;
            }

            Transform triggerAreaTransform = triggerAreaMonoBehaviour.transform;

            if (transformComponent.Cached.WorldPosition != triggerAreaTransform.position)
                triggerAreaTransform.position = transformComponent.Cached.WorldPosition;

            if (transformComponent.Cached.WorldRotation != triggerAreaTransform.rotation)
                triggerAreaTransform.rotation = transformComponent.Cached.WorldRotation;

            if (!triggerAreaMonoBehaviour.BoxCollider.enabled)
                triggerAreaMonoBehaviour.BoxCollider.enabled = true; // TODO: Disable when returning to pool...
        }

        public void FinalizeComponents(in Query query)
        {
            throw new NotImplementedException();
        }
    }
}
