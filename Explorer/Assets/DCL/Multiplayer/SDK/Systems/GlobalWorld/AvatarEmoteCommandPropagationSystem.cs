using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using CharacterEmoteSystem = DCL.AvatarRendering.Emotes.Play.CharacterEmoteSystem;

namespace DCL.Multiplayer.SDK.Systems.GlobalWorld
{
    /// <summary>
    ///     Transfers the one-shot emote start/stop events recorded by <see cref="CharacterEmoteSystem" />
    ///     (on the previous frame) to the scene-world <see cref="AvatarEmoteCommandComponent" /> exactly once,
    ///     so WriteAvatarEmoteCommandSystem can append them to the scene.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(CharacterEmoteSystem))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class AvatarEmoteCommandPropagationSystem : BaseUnityLoopSystem
    {
        public AvatarEmoteCommandPropagationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateEmoteCommandDataComponentQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateEmoteCommandDataComponent(in PlayerCRDTEntity playerCRDTEntity, ref CharacterEmoteComponent emoteComponent)
        {
            if (!emoteComponent.PendingStart.IsSet && !emoteComponent.PendingStop.IsSet) return;

            if (!playerCRDTEntity.AssignedToScene)
            {
                // There is no scene to report to: drop the events instead of letting them go stale.
                emoteComponent.PendingStart = default(EmoteStartEvent);
                emoteComponent.PendingStop = default(EmoteStopEvent);
                return;
            }

            World sceneWorld = playerCRDTEntity.SceneFacade.EcsExecutor.World;

            bool componentFound = sceneWorld.TryGet(playerCRDTEntity.SceneWorldEntity, out AvatarEmoteCommandComponent emoteCommand);

            // Latest transition wins: if the scene world has not consumed the previous events yet (throttled
            // scene), they are overwritten rather than reordered — the snapshot below stays consistent.
            emoteCommand.StopEvent = emoteComponent.PendingStop;
            emoteCommand.StartEvent = emoteComponent.PendingStart;

            if (emoteComponent.PendingStart.IsSet)
            {
                emoteCommand.PlayingEmote = emoteComponent.PendingStart.Urn;
                emoteCommand.LoopingEmote = emoteComponent.PendingStart.Loop;
                emoteCommand.IsPlaying = true;
            }
            else
                emoteCommand.IsPlaying = false;

            emoteCommand.IsDirty = true;

            // The events transfer exactly once; leaving them set would re-dirty the scene component every frame
            // (and previously caused duplicate start appends for every frame an emote intent stayed alive).
            emoteComponent.PendingStart = default(EmoteStartEvent);
            emoteComponent.PendingStop = default(EmoteStopEvent);

            if (componentFound)
                sceneWorld.Set(playerCRDTEntity.SceneWorldEntity, emoteCommand);
            else
                sceneWorld.Add(playerCRDTEntity.SceneWorldEntity, emoteCommand);
        }
    }
}
