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
    [UpdateAfter(typeof(PlayerProfileDataPropagationSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_SDK_EMOTE_COMMAND_DATA)]
    public partial class AvatarEmoteCommandPropagationSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription REMOVAL_QUERY = new QueryDescription().WithNone<PlayerProfileDataComponent>().WithAll<AvatarEmoteCommandComponent>();
        private readonly IEmoteCache emoteCache;

        public AvatarEmoteCommandPropagationSystem(World world, IEmoteCache emoteCache) : base(world)
        {
            this.emoteCache = emoteCache;
        }

        protected override void Update(float t)
        {
            UpdateEmoteCommandDataComponentQuery(World);
            CreateEmoteCommandDataComponentQuery(World);

            HandlePlayerDisconnectQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(AvatarEmoteCommandComponent))]
        private void CreateEmoteCommandDataComponent(in Entity entity, ref PlayerProfileDataComponent playerProfileData, ref CharacterEmoteIntent emoteIntent)
        {
            ISceneFacade sceneFacade = playerProfileData.SceneFacade;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            SceneEcsExecutor sceneEcsExecutor = sceneFacade.EcsExecutor;

            if (emoteCache.TryGetEmote(emoteIntent.EmoteId.Shorten(), out IEmote emote))
            {
                AvatarEmoteCommandComponent emoteCommandComponent = new ()
                {
                    IsDirty = true,
                    PlayingEmote = emoteIntent.EmoteId,
                    LoopingEmote = emote.IsLooping(),
                };

                // External world access should be always synchronized (Global World calls into Scene World)
                using (sceneEcsExecutor.Sync.GetScope())
                    sceneEcsExecutor.World.Add(playerProfileData.SceneWorldEntity, emoteCommandComponent);

                World.Add(entity, emoteCommandComponent);
            }
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandDataComponent(ref PlayerProfileDataComponent playerProfileData, ref CharacterEmoteIntent emoteIntent, ref AvatarEmoteCommandComponent emoteCommandComponent)
        {
            ISceneFacade sceneFacade = playerProfileData.SceneFacade;

            if (sceneFacade.IsEmpty || !sceneFacade.SceneStateProvider.IsCurrent)
                return;

            SceneEcsExecutor sceneEcsExecutor = playerProfileData.SceneFacade.EcsExecutor;

            if (emoteCache.TryGetEmote(emoteIntent.EmoteId.Shorten(), out IEmote emote))
            {
                emoteCommandComponent.IsDirty = true;
                emoteCommandComponent.PreviousEmote = emoteCommandComponent.PlayingEmote;
                emoteCommandComponent.PlayingEmote = emoteIntent.EmoteId;
                emoteCommandComponent.LoopingEmote = emote.IsLooping();

                // External world access should be always synchronized (Global World calls into Scene World)
                using (sceneEcsExecutor.Sync.GetScope())
                    sceneEcsExecutor.World.Set(playerProfileData.SceneWorldEntity, emoteCommandComponent);
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandlePlayerDisconnect(PlayerProfileDataComponent playerProfileDataComponent, ref AvatarEmoteCommandComponent emoteCommandComponent)
        {
            SceneEcsExecutor sceneEcsExecutor = playerProfileDataComponent.SceneFacade.EcsExecutor;

            // External world access should be always synchronized (Global World calls into Scene World)
            using (sceneEcsExecutor.Sync.GetScope())
                sceneEcsExecutor.World.Set(playerProfileDataComponent.SceneWorldEntity, emoteCommandComponent);
        }
    }
}
