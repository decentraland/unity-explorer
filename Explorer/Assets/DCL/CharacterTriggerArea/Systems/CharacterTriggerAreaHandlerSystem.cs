using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class CharacterTriggerAreaHandlerSystem : BaseUnityLoopSystem
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

            UpdateCharacterTriggerAreaQuery(World);
        }

        [Query]
        private void UpdateCharacterTriggerArea(ref TransformComponent transformComponent, ref CharacterTriggerAreaComponent triggerAreaComponent)
        {
            if (triggerAreaComponent.TryDispose(sceneStateProvider))
                return;

            triggerAreaComponent.TryAssignArea(poolRegistry, mainPlayerTransform);
            triggerAreaComponent.TryUpdateTransform(ref transformComponent);
        }
    }
}
