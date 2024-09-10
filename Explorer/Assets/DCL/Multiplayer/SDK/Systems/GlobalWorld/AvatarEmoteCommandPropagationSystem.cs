using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.Play.CharacterEmoteSystem;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CharacterEmoteSystem))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class AvatarEmoteCommandPropagationSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteStorage emoteStorage;

        public AvatarEmoteCommandPropagationSystem(World world, IEmoteStorage emoteStorage) : base(world)
        {
            this.emoteStorage = emoteStorage;
        }

        protected override void Update(float t)
        {
            UpdateEmoteCommandDataComponentQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandDataComponent(PlayerCRDTEntity playerCRDTEntity, CharacterEmoteIntent emoteIntent)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;
            World sceneWorld = sceneEcsExecutor.World;

            bool componentFound = sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent emoteCommandComponent);

            if (!componentFound)
                emoteCommandComponent = new AvatarEmoteCommandComponent();

            if (emoteStorage.TryGetElement(emoteIntent.EmoteId.Shorten(), out IEmote emote))
            {
                emoteCommandComponent.IsDirty = true;
                emoteCommandComponent.PlayingEmote = emoteIntent.EmoteId;
                emoteCommandComponent.LoopingEmote = emote.IsLooping();

                if (componentFound)
                    sceneWorld.Set(playerCRDTEntity.SceneWorldEntity, emoteCommandComponent);
                else
                    sceneWorld.Add(playerCRDTEntity.SceneWorldEntity, emoteCommandComponent);
            }
        }
    }
}
