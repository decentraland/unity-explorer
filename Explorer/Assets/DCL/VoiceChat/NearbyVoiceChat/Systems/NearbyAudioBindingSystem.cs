using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Friends.UserBlocking;
using DCL.Profiles;
using DCL.VoiceChat.Nearby.Audio;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using Global.AppArgs;
using LiveKit.Rooms.Streaming;
using LiveKit.Rooms.Streaming.Audio;
using RichTypes;
using System;
using System.Collections.Generic;

namespace DCL.VoiceChat.Nearby.Systems
{
    /// <summary>
    ///     Owns the creation part of the Nearby audio-source lifecycle.
    ///     For every avatar entity (<see cref="Profile"/> + <see cref="AvatarBase"/> + <see cref="NearbyAudioStreamerComponent"/> + <see cref="InAudibleRangeTag"/>)
    ///     the system iterates the snapshotted sids on the entity itself and materializes an audio-source entity per <c>(walletId, sid)</c> pair that does not yet have one.
    ///     Throttled to a fixed budget per frame so a crowd ramp-up does not spike a single tick.
    /// </summary>
    [UpdateInGroup(typeof(NearbyVoiceChatGroup))]
    [UpdateAfter(typeof(NearbyAudibleRangeSystem))]
    [UpdateBefore(typeof(NearbyAudioPositionSystem))]
    public partial class NearbyAudioBindingSystem : BaseUnityLoopSystem
    {
        internal const int MAX_CREATIONS_PER_FRAME = 10;

        private readonly INearbyAudioStreamRegistry registry;
        private readonly HashSet<StreamKey> bindings;
        private readonly IUserBlockingCache userBlockingCache;
        private readonly NearbyVoiceChatStateModel stateModel;
        private readonly INearbyAudioSourceFactory sourceFactory;


        internal NearbyAudioBindingSystem(World world, INearbyAudioStreamRegistry registry, HashSet<StreamKey> bindings, IUserBlockingCache userBlockingCache, NearbyVoiceChatStateModel stateModel, INearbyAudioSourceFactory sourceFactory) : base(world)
        {
            this.registry = registry;
            this.bindings = bindings;
            this.userBlockingCache = userBlockingCache;
            this.stateModel = stateModel;
            this.sourceFactory = sourceFactory;
        }

        protected override void OnDispose()
        {
            sourceFactory.DisposeRoot();
        }

        private static bool HasExitTestSkipAudioSourceCreate()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dashed = "--" + AppArgsFlags.EXIT_TEST_SKIP_AUDIO_SOURCE_CREATE;
            for (var i = 0; i < args.Length; i++)
                if (args[i] == dashed)
                    return true;
            return false;
        }

        // EXIT-DELAY BISECTION (#8764): skip the registry.GetActiveStream(key) call. The earlier
        // SKIP_AUDIO_SOURCE_CREATE flag only skipped LivekitAudioSource.Create()/.Play(); it did
        // NOT skip GetActiveStream, which lazily constructs an AudioStream and performs a
        // synchronous FFI request. If this flag makes the exit consistently fast, GetActiveStream
        // is what attaches the tokio workers to the IL2CPP runtime.
        private static bool HasExitTestSkipGetActiveStream()
        {
            string[] args = Environment.GetCommandLineArgs();
            string dashed = "--" + AppArgsFlags.EXIT_TEST_SKIP_GET_ACTIVE_STREAM;
            for (var i = 0; i < args.Length; i++)
                if (args[i] == dashed)
                    return true;
            return false;
        }

        protected override void Update(float t)
        {
            // Listening gate: skip the entire avatar query when nearby chat is SUPPRESSED or DISABLED.
            // Cleanup system handles the symmetric teardown of any already-bound entities.
            if (stateModel.IsListeningDisabled) return;

            CreateAndBindAudioSourcesToStreamersQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        [All(typeof(AvatarBase), typeof(NearbyAudioStreamerComponent), typeof(InAudibleRangeTag))]
        private void CreateAndBindAudioSourcesToStreamers(Entity avatarEntity, in Profile profile, in NearbyAudioStreamerComponent nearby)
        {
            string walletId = profile.UserId;
            if (string.IsNullOrEmpty(walletId)) return;

            // Skip blocked identities. Cleanup system handles already-bound entities; this filter prevents creation in the first place.
            if (userBlockingCache.UserIsBlocked(walletId)) return;

            // Sids ride on the entity itself — no registry call on the per-avatar hot path.
            // Bridge system guarantees SidsSnapshot is non-null for any entity that has the component.
            foreach (string sid in nearby.StreamSidsSnapshot)
            {
                var key = new StreamKey(walletId, sid);

                if (!bindings.Contains(key))
                {
                    // EXIT-DELAY BISECTION (#8764): when --exit-test-skip-get-active-stream is set,
                    // bypass the registry.GetActiveStream(key) call entirely. That call lazily
                    // constructs a LiveKit AudioStream and performs a synchronous FFI request;
                    // we suspect it is what attaches tokio workers to the IL2CPP runtime.
                    if (HasExitTestSkipGetActiveStream())
                        continue;

                    Weak<AudioStream> stream = registry.GetActiveStream(key);

                    // Track was unsubscribed between collection (snapshot read) and resolve (GetActiveStream); skip to avoid a one-frame ghost source.
                    if (!stream.Resource.Has) continue;

                    // EXIT-DELAY BISECTION (#8764): when --exit-test-skip-audio-source-create is set,
                    // skip the LivekitAudioSource creation/Play() that triggers OnAudioFilterRead → FFI.
                    // The system stays registered and iterates avatars normally — only the Create call
                    // and the resulting NearbyAudioSourceComponent are bypassed. Used to isolate whether
                    // the active AudioSource binding is what keeps livekit_ffi tokio workers attached
                    // to the IL2CPP runtime, or whether the registration of the system alone is enough.
                    if (HasExitTestSkipAudioSourceCreate())
                        continue;

                    LivekitAudioSource source = sourceFactory.Create(key, stream);

                    World.Create(new NearbyAudioSourceComponent(key, avatarEntity, source));
                    bindings.Add(key);
                }
            }
        }
    }
}
