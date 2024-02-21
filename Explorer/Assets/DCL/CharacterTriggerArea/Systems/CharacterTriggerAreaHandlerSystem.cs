using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
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

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<CharacterTriggerAreaComponent>(in HandleEntityDestruction_QueryDescription);
            World.Remove<CharacterTriggerAreaComponent>(in HandleComponentRemoval_QueryDescription);

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
                    // We don't use Dispose() here because we want to keep the configured OnEnter/OnExit events
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

                    if (characterTriggerAreaComponent.TargetOnlyMainPlayer)
                        triggerAreaMonoBehaviour.TargetTransform = mainPlayerReferences.MainPlayerTransform.Transform;
                }

                // Values that may have been updated from the SDK scene
                triggerAreaMonoBehaviour.ClearEvents();
                triggerAreaMonoBehaviour.OnEnteredTrigger += characterTriggerAreaComponent.OnEnteredTrigger;
                triggerAreaMonoBehaviour.OnExitedTrigger += characterTriggerAreaComponent.OnExitedTrigger;
                characterTriggerAreaComponent.MonoBehaviour.BoxCollider.size = characterTriggerAreaComponent.AreaSize;
            }

            Transform triggerAreaTransform = triggerAreaMonoBehaviour.transform;

            if (transformComponent.Cached.WorldPosition != triggerAreaTransform.position)
                triggerAreaTransform.position = transformComponent.Cached.WorldPosition;

            if (transformComponent.Cached.WorldRotation != triggerAreaTransform.rotation)
                triggerAreaTransform.rotation = transformComponent.Cached.WorldRotation;

            if (!triggerAreaMonoBehaviour.BoxCollider.enabled)
                triggerAreaMonoBehaviour.BoxCollider.enabled = true;
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(PBAvatarModifierArea))]
        private void HandleComponentRemoval(ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);
        }

        [Query]
        private void FinalizeComponents(in Entity entity, ref CharacterTriggerAreaComponent component)
        {
            poolRegistry.Release(component.MonoBehaviour);
            World.Remove<CharacterTriggerAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
