using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Single owner of structural changes for Nearby audio-source entities.
    ///     Per tick scans all audio entities and tears them down on any of:
    ///     - <b>Trigger #1 (avatar gone)</b> — linked avatar entity is dead, flagged with
    ///       <see cref="DeleteEntityIntention"/>, or has lost <see cref="AvatarBase"/>;
    ///     - <b>Trigger #2 (stream gone)</b> — registry no longer reports the bound <c>(walletId, sid)</c>;
    ///     - <b>Trigger #3 (blocked)</b> — <see cref="IUserBlockingCache.UserIsBlocked"/> returns <c>true</c>
    ///       for the bound <c>walletId</c> (covers both "I block them" and "they block me");
    ///     - <b>Trigger #4 (listening gate)</b> — <see cref="NearbyVoiceChatStateModel"/> is in
    ///       <see cref="NearbyVoiceChatState.SUPPRESSED"/> or <see cref="NearbyVoiceChatState.DISABLED"/>;
    ///       fires for every audio entity regardless of per-entity flags.
    ///     Teardown is atomic: <see cref="NearbyAudioSourceFactory.Dispose"/> →
    ///     <c>bindings.Remove</c> → <c>World.Destroy</c>.
    ///     Implements <see cref="IFinalizeWorldSystem"/> to dispose any survivors at world finalization.
    /// </summary>
    [UpdateInGroup(typeof(AvatarGroup))]
    [UpdateAfter(typeof(NearbyAudioPositionSystem))]
    public partial class NearbyAudioCleanupSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly INearbyAudioStreamRegistry registry;

        private readonly Dictionary<StreamKey, Entity> bindings;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly NearbyAudioSourceFactory sourceFactory;
        private readonly List<Entity> entitiesToCleanUp = new (16);

        internal NearbyAudioCleanupSystem(World world, INearbyAudioStreamRegistry registry, Dictionary<StreamKey, Entity> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, NearbyAudioSourceFactory sourceFactory) : base(world)
        {
            this.registry = registry;
            this.bindings = bindings;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
        }

        protected override void Update(float t)
        {
            entitiesToCleanUp.Clear();
            FlagDeadAudioEntitiesQuery(World);

            // Structural changes only after the query has released all ref/in/out reads — see CLAUDE.md §5.
            foreach (Entity audioEntity in entitiesToCleanUp)
                TearDown(audioEntity);
        }

        public void FinalizeComponents(in Query query)
        {
            entitiesToCleanUp.Clear();
            CollectAllAudioEntitiesQuery(World);

            foreach (Entity audioEntity in entitiesToCleanUp)
                DisposeSourceOnly(audioEntity);

            bindings.Clear();
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void FlagDeadAudioEntities(Entity audioEntity, ref NearbyAudioSourceComponent comp)
        {
            // Listening-gate trigger fires for every entity unconditionally — checked first as the cheapest predicate.
            if (stateModel.IsListeningDisabled || userBlockingCache.UserIsBlocked(comp.Key.identity) ||
                registry.IsStreamGone(comp.Key) || IsAvatarGone(comp.AvatarEntity))
            {
                entitiesToCleanUp.Add(audioEntity);
            }

            return;
            bool IsAvatarGone(Entity avatarEntity) =>
                !World.IsAlive(avatarEntity) || World.Has<DeleteEntityIntention>(avatarEntity) || !World.Has<AvatarBase>(avatarEntity);
        }

        [Query]
        private void CollectAllAudioEntities(Entity audioEntity, ref NearbyAudioSourceComponent _)
        {
            entitiesToCleanUp.Add(audioEntity);
        }

        private void TearDown(Entity audioEntity)
        {
            NearbyAudioSourceComponent comp = World.Get<NearbyAudioSourceComponent>(audioEntity);
            sourceFactory.Dispose(comp.LivekitAudioSource);
            bindings.Remove(comp.Key);
            World.Destroy(audioEntity);
        }

        private void DisposeSourceOnly(Entity audioEntity)
        {
            NearbyAudioSourceComponent comp = World.Get<NearbyAudioSourceComponent>(audioEntity);
            sourceFactory.Dispose(comp.LivekitAudioSource);
        }
    }
}
