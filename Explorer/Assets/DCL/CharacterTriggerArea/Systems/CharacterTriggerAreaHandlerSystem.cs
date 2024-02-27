using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.Utilities;
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
    public partial class CharacterTriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CharacterTriggerArea> poolRegistry;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly Transform mainPlayerTransform;
        private readonly ISceneStateProvider sceneStateProvider;

        public CharacterTriggerAreaHandlerSystem(World world, IComponentPool<CharacterTriggerArea> poolRegistry, ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy, ISceneStateProvider sceneStateProvider, ICharacterObject characterObject) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.sceneStateProvider = sceneStateProvider;
            mainPlayerTransform = characterObject.Transform;
        }

        protected override void Update(float t)
        {
            if (!mainPlayerAvatarBaseProxy.Configured) return;

            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
            World.Remove<CharacterTriggerAreaComponent>(in HandleEntityDestruction_QueryDescription);
            World.Remove<CharacterTriggerAreaComponent>(in HandleComponentRemoval_QueryDescription);

            UpdateCharacterTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateCharacterTriggerArea(ref TransformComponent transformComponent, ref CharacterTriggerAreaComponent triggerAreaComponent)
        {
            CharacterTriggerArea triggerAreaMonoBehaviour = triggerAreaComponent.MonoBehaviour;

            if (!sceneStateProvider.IsCurrent)
            {
                triggerAreaMonoBehaviour?.Dispose();
                return;
            }

            if (triggerAreaComponent.IsDirty)
            {
                triggerAreaComponent.IsDirty = false;

                if (triggerAreaMonoBehaviour == null)
                {
                    triggerAreaMonoBehaviour = poolRegistry.Get();

                    if (triggerAreaComponent.TargetOnlyMainPlayer)
                        triggerAreaMonoBehaviour.TargetTransform = mainPlayerTransform;

                    triggerAreaComponent.MonoBehaviour = triggerAreaMonoBehaviour;
                }

                triggerAreaComponent.MonoBehaviour.BoxCollider.size = triggerAreaComponent.AreaSize;
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
