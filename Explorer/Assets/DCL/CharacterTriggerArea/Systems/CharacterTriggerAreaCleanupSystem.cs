using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterTriggerArea.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class CharacterTriggerAreaCleanupSystem : BaseUnityLoopSystem
    {
        public CharacterTriggerAreaCleanupSystem(World world) : base(world) { }

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
