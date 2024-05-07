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

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerCRDTEntitiesHandlerSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_EMOTE_COMMAND_DATA)]
    public partial class AvatarEmoteCommandPropagationSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteCache emoteCache;

        public AvatarEmoteCommandPropagationSystem(World world, IEmoteCache emoteCache) : base(world)
        {
            this.emoteCache = emoteCache;
        }

        protected override void Update(float t)
        {
            UpdateEmoteCommandDataComponentQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandDataComponent(ref PlayerCRDTEntity playerCRDTEntity, ref CharacterEmoteIntent emoteIntent)
        {
            SceneEcsExecutor sceneEcsExecutor = playerCRDTEntity.SceneFacade.EcsExecutor;
            World sceneWorld = sceneEcsExecutor.World;

            bool componentFound = sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent emoteCommandComponent);

            if (!componentFound)
                emoteCommandComponent = new AvatarEmoteCommandComponent();

            if (emoteCache.TryGetEmote(emoteIntent.EmoteId.Shorten(), out IEmote emote))
            {
                emoteCommandComponent.IsDirty = true;
                emoteCommandComponent.PlayingEmote = emoteIntent.EmoteId;
                emoteCommandComponent.LoopingEmote = emote.IsLooping();

                // External world access should be always synchronized (Global World calls into Scene World)
                // using (sceneEcsExecutor.Sync.GetScope())
                {
                    if (componentFound)
                        sceneWorld.Set(playerCRDTEntity.SceneWorldEntity, emoteCommandComponent);
                    else
                        sceneWorld.Add(playerCRDTEntity.SceneWorldEntity, emoteCommandComponent);
                }
            }
        }
    }
}
