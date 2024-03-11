using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Emotes;
using DCL.DebugUtilities;
using ECS.Abstract;
using System;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterAnimationSystem))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteRepository emoteRepository;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public CharacterEmoteSystem(World world, IEmoteRepository emoteRepository, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            this.emoteRepository = emoteRepository;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        protected override void Update(float t)
        {
            TriggerEmotesQuery(World);
        }

        [Query]
        private void TriggerEmotes(in Entity entity, ref CharacterAnimationComponent animationComponent, in CharacterEmoteIntent emoteIntent)
        {
            string emoteId = emoteIntent.EmoteId;

            if (!emoteRepository.Exists(emoteId)) return;

            var emoteData = emoteRepository.Get(emoteId);

            animationComponent.States.IsEmote = true;
            animationComponent.States.EmoteClip = emoteData.avatarClip;
            animationComponent.States.EmoteLoop = true;

            World.Remove<CharacterEmoteIntent>(entity);
        }
    }
}
