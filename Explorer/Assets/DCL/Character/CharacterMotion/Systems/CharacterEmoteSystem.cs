using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Character.CharacterMotion.Emotes;
using DCL.CharacterMotion.Components;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Optimization.ThreadSafePool;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.EMOTE)]
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    [UpdateBefore(typeof(CharacterAnimationSystem))]
    public partial class CharacterEmoteSystem : BaseUnityLoopSystem
    {
        private readonly IEmoteCache emoteCache;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly string reportCategory;
        private readonly EmotePlayer emotePlayer;

        public CharacterEmoteSystem(World world, IEmoteCache emoteCache, IDebugContainerBuilder debugContainerBuilder) : base(world)
        {
            this.emoteCache = emoteCache;
            this.debugContainerBuilder = debugContainerBuilder;
            reportCategory = GetReportCategory();
            emotePlayer = new EmotePlayer(reportCategory);
        }

        protected override void Update(float t)
        {
            TriggerEmotesQuery(World);
        }

        [Query]
        private void TriggerEmotes(in Entity entity, ref CharacterAnimationComponent animationComponent, in CharacterEmoteIntent emoteIntent, in AvatarBase avatarBase)
        {
            string emoteId = emoteIntent.EmoteId;

            if (emoteCache.TryGetEmote(emoteId, out IEmote emote))
            {
                // emote failed to load? remove intent
                if (emote.ManifestResult is { IsInitialized: true, Exception: not null })
                {
                    ReportHub.LogError(reportCategory, $"Cant play emote {emoteId} since it failed loading \n {emote.ManifestResult}");
                    World.Remove<CharacterEmoteIntent>(entity);
                    return;
                }

                StreamableLoadingResult<WearableAsset>? streamableAsset = emote.WearableAssetResults[0];

                // the emote is still loading? dont remove the intent yet, wait for it
                if (streamableAsset == null) return;
                if (!streamableAsset.Value.Succeeded) return;
                if (streamableAsset.Value.Exception != null) return;

                GameObject? mainAsset = streamableAsset.Value.Asset?.GetMainAsset<GameObject>();

                if (mainAsset == null) return;

                if (!emotePlayer.Play(mainAsset, emote.IsLooping(), in avatarBase, ref animationComponent))
                    ReportHub.LogWarning(reportCategory, $"Emote {emote.Model.Asset.metadata.name} cant be played, AB version: {emote.ManifestResult?.Asset?.dto.version} should be >= 16");

                World.Remove<CharacterEmoteIntent>(entity);
            }
            else
            {
                // if it does not even exist in the cache, then this failed completely
                World.Remove<CharacterEmoteIntent>(entity);
            }
        }
    }
}
