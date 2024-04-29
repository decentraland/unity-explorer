using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterTriggerArea.Components;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    public partial class CharacterTriggerAreaCleanUpRegisteredCollisionsSystem : BaseUnityLoopSystem
    {
        internal CharacterTriggerAreaCleanUpRegisteredCollisionsSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            ClearDetectedCharactersCollectionQuery(World);
        }

        [Query]
        private void ClearDetectedCharactersCollection(ref CharacterTriggerAreaComponent component)
        {
            component.MonoBehaviour?.Clear();
        }
    }
}
