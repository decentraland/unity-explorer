using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterMotion.Components;
using ECS.Abstract;
using System;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterAnimationSystem))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        public CharacterEmoteSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            TriggerEmotesQuery(World);
        }

        [Query]
        private void TriggerEmotes(in Entity entity, in CharacterEmoteIntent emoteIntent)
        {
            // if (checkIfEmoteExists)
            //      changeAnimationState
            //      removeIntent
            // else
            //      keep intent?
        }
    }
}
