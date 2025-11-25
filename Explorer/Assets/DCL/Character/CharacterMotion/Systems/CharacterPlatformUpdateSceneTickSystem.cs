using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(RotateCharacterSystem))]
    public partial class CharacterPlatformUpdateSceneTickSystem : BaseUnityLoopSystem
    {
        private readonly IScenesCache scenesCache;

        public CharacterPlatformUpdateSceneTickSystem(World world, IScenesCache scenesCache) : base(world)
        {
            this.scenesCache = scenesCache;
        }

        protected override void Update(float t)
        {
            UpdateTickQuery(World);
        }

        [Query]
        private void UpdateTick(ref CharacterPlatformComponent platformComponent)
        {
            ISceneFacade? currentScene = scenesCache.CurrentScene.Value;
            platformComponent.LastUpdateTick = currentScene?.SceneStateProvider.TickNumber ?? 0;
        }
    }
}
