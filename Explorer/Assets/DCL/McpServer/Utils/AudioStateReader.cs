using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.McpServer.Core;
using DCL.SDKComponents.AudioSources;
using DCL.SDKComponents.MediaStream;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    public enum AudioKind : byte
    {
        AudioSource,
        AudioStream,
        VideoPlayer,
    }

    /// <summary>The media states the scene's audio events report, plus one for a component the client has not created yet.</summary>
    public enum AudioPlaybackState : byte
    {
        NotCreated,
        None,
        Loading,
        Ready,
        Playing,
        Buffering,
        Seeking,
        Paused,
        Error,
    }

    /// <summary>One sound emitter of the scene as the client runs it now.</summary>
    public struct AudioEntry
    {
        public int EntityId;
        public int CrdtEntity;
        public AudioKind Kind;
        public string? Source;
        public AudioPlaybackState State;
        public bool Playing;
        public float AuthoredVolume;
        public float? LiveVolume;
        public bool Loop;
        public float? Pitch;
        public bool Spatial;
        public float? MinDistance;
        public float? MaxDistance;
        public float? CurrentTime;
        public float? Duration;
        public bool IsLiveStream;
        public Vector3? Position;

        /// <summary>Metres from the player; positive infinity when the entity has no transform.</summary>
        public float Distance;
    }

    /// <summary>Reads the scene world's audio emitters and shapes them for the agent, nearest first.</summary>
    public static class AudioStateReader
    {
        private static readonly QueryDescription AUDIO_SOURCES = new QueryDescription().WithAll<PBAudioSource>().WithNone<DeleteEntityIntention>();
        private static readonly QueryDescription AUDIO_STREAMS = new QueryDescription().WithAll<PBAudioStream>().WithNone<DeleteEntityIntention>();
        private static readonly QueryDescription VIDEO_PLAYERS = new QueryDescription().WithAll<PBVideoPlayer>().WithNone<DeleteEntityIntention>();

        private static readonly Comparison<AudioEntry> NEAREST_FIRST = static (a, b) =>
        {
            int byDistance = a.Distance.CompareTo(b.Distance);
            return byDistance != 0 ? byDistance : a.CrdtEntity.CompareTo(b.CrdtEntity);
        };

        public static void Collect(World sceneWorld, Vector3 playerPosition, List<AudioEntry> into)
        {
            sceneWorld.Query(in AUDIO_SOURCES, (Entity entity, ref PBAudioSource sdkSource) =>
            {
                AudioEntry entry = Describe(sceneWorld, entity, playerPosition, AudioKind.AudioSource);
                entry.Source = sdkSource.AudioClipUrl;
                entry.AuthoredVolume = sdkSource.GetVolume();
                entry.Loop = sdkSource.HasLoop && sdkSource.Loop;
                entry.Pitch = sdkSource.HasPitch ? sdkSource.Pitch : 1f;
                entry.Spatial = !(sdkSource.HasGlobal && sdkSource.Global);

                if (sceneWorld.TryGet(entity, out AudioSourceComponent component))
                {
                    entry.State = FromMediaState(component.GetMediaState());
                    AudioSource? audioSource = component.AudioSource;

                    if (audioSource != null)
                    {
                        entry.Playing = audioSource.isPlaying;
                        entry.LiveVolume = audioSource.volume;
                        entry.Spatial = audioSource.spatialBlend > 0f;
                        entry.MinDistance = audioSource.minDistance;
                        entry.MaxDistance = audioSource.maxDistance;

                        if (audioSource.clip != null)
                        {
                            entry.CurrentTime = audioSource.time;
                            entry.Duration = audioSource.clip.length;
                        }
                    }
                }

                into.Add(entry);
            });

            sceneWorld.Query(in AUDIO_STREAMS, (Entity entity, ref PBAudioStream sdkStream) =>
            {
                AudioEntry entry = Describe(sceneWorld, entity, playerPosition, AudioKind.AudioStream);
                entry.Source = sdkStream.Url;
                entry.AuthoredVolume = sdkStream.HasVolume ? sdkStream.Volume : MediaPlayerComponent.DEFAULT_VOLUME;
                entry.Loop = true;
                entry.Spatial = sdkStream.HasSpatial && sdkStream.Spatial;
                entry.MinDistance = sdkStream.HasSpatialMinDistance ? sdkStream.SpatialMinDistance : null;
                entry.MaxDistance = sdkStream.HasSpatialMaxDistance ? sdkStream.SpatialMaxDistance : null;
                entry.IsLiveStream = true;
                ReadMediaPlayer(sceneWorld, entity, ref entry);
                into.Add(entry);
            });

            sceneWorld.Query(in VIDEO_PLAYERS, (Entity entity, ref PBVideoPlayer sdkVideo) =>
            {
                AudioEntry entry = Describe(sceneWorld, entity, playerPosition, AudioKind.VideoPlayer);
                entry.Source = sdkVideo.Src;
                entry.AuthoredVolume = sdkVideo.HasVolume ? sdkVideo.Volume : MediaPlayerComponent.DEFAULT_VOLUME;
                entry.Loop = sdkVideo.HasLoop && sdkVideo.Loop;
                entry.Pitch = sdkVideo.HasPlaybackRate ? sdkVideo.PlaybackRate : MediaPlayerComponent.DEFAULT_PLAYBACK_RATE;
                entry.Spatial = sdkVideo.HasSpatial && sdkVideo.Spatial;
                entry.MinDistance = sdkVideo.HasSpatialMinDistance ? sdkVideo.SpatialMinDistance : null;
                entry.MaxDistance = sdkVideo.HasSpatialMaxDistance ? sdkVideo.SpatialMaxDistance : null;
                ReadMediaPlayer(sceneWorld, entity, ref entry);
                into.Add(entry);
            });

            into.Sort(NEAREST_FIRST);
        }

        public static AudioPlaybackState FromMediaState(MediaState state) =>
            state switch
            {
                MediaState.MsError => AudioPlaybackState.Error,
                MediaState.MsLoading => AudioPlaybackState.Loading,
                MediaState.MsReady => AudioPlaybackState.Ready,
                MediaState.MsPlaying => AudioPlaybackState.Playing,
                MediaState.MsBuffering => AudioPlaybackState.Buffering,
                MediaState.MsSeeking => AudioPlaybackState.Seeking,
                MediaState.MsPaused => AudioPlaybackState.Paused,
                _ => AudioPlaybackState.None,
            };

        public static AudioPlaybackState FromVideoState(VideoState state) =>
            state switch
            {
                VideoState.VsError => AudioPlaybackState.Error,
                VideoState.VsLoading => AudioPlaybackState.Loading,
                VideoState.VsReady => AudioPlaybackState.Ready,
                VideoState.VsPlaying => AudioPlaybackState.Playing,
                VideoState.VsBuffering => AudioPlaybackState.Buffering,
                VideoState.VsSeeking => AudioPlaybackState.Seeking,
                VideoState.VsPaused => AudioPlaybackState.Paused,
                _ => AudioPlaybackState.None,
            };

        /// <summary>Shapes already-sorted entries: filters, limits and counts them.</summary>
        public static JObject Build(List<AudioEntry> entries, bool onlyPlaying, int limit, bool sceneIsCurrent, string? note)
        {
            var playing = 0;
            var audioSources = 0;
            var streams = 0;
            var videos = 0;
            var matched = 0;
            var shaped = new JArray();

            foreach (AudioEntry entry in entries)
            {
                if (entry.Playing) playing++;

                switch (entry.Kind)
                {
                    case AudioKind.AudioSource: audioSources++; break;
                    case AudioKind.AudioStream: streams++; break;
                    default: videos++; break;
                }

                if (onlyPlaying && !entry.Playing)
                    continue;

                matched++;

                if (shaped.Count < limit)
                    shaped.Add(ToJson(in entry));
            }

            var result = new JObject
            {
                ["sceneIsCurrent"] = sceneIsCurrent,
                ["note"] = note != null ? new JValue(note) : JValue.CreateNull(),
                ["summary"] = new JObject
                {
                    ["total"] = entries.Count,
                    ["playing"] = playing,
                    ["audioSources"] = audioSources,
                    ["streams"] = streams,
                    ["videos"] = videos,
                },
                ["returned"] = shaped.Count,
                ["truncated"] = shaped.Count < matched,
                ["entries"] = shaped,
            };

            return result;
        }

        public static JObject ToJson(in AudioEntry entry) =>
            new ()
            {
                ["entityId"] = entry.EntityId,
                ["crdtEntity"] = entry.CrdtEntity,
                ["kind"] = McpWireEnum<AudioKind>.ToWire(entry.Kind),
                ["source"] = entry.Source != null ? new JValue(entry.Source) : JValue.CreateNull(),
                ["state"] = McpWireEnum<AudioPlaybackState>.ToWire(entry.State),
                ["playing"] = entry.Playing,
                ["volume"] = new JObject
                {
                    ["authored"] = entry.AuthoredVolume,
                    ["live"] = Optional(entry.LiveVolume),
                },
                ["loop"] = entry.Loop,
                ["pitch"] = Optional(entry.Pitch),
                ["spatial"] = entry.Spatial,
                ["minDistance"] = Optional(entry.MinDistance),
                ["maxDistance"] = Optional(entry.MaxDistance),
                ["currentTime"] = Optional(entry.CurrentTime),
                ["duration"] = Optional(entry.Duration),
                ["liveStream"] = entry.IsLiveStream,
                ["position"] = entry.Position.HasValue ? entry.Position.Value.ToVector() : JValue.CreateNull(),
                ["distance"] = float.IsPositiveInfinity(entry.Distance) ? JValue.CreateNull() : new JValue(Math.Round(entry.Distance, 2)),
            };

        private static AudioEntry Describe(World sceneWorld, Entity entity, Vector3 playerPosition, AudioKind kind)
        {
            var entry = new AudioEntry
            {
                EntityId = entity.Id,
                CrdtEntity = sceneWorld.TryGet(entity, out CRDTEntity crdt) ? crdt.Id : -1,
                Kind = kind,
                State = AudioPlaybackState.NotCreated,
                Distance = float.PositiveInfinity,
            };

            if (sceneWorld.TryGet(entity, out TransformComponent transform))
            {
                entry.Position = transform.Cached.WorldPosition;
                entry.Distance = Vector3.Distance(playerPosition, transform.Cached.WorldPosition);
            }

            return entry;
        }

        /// <summary>A player the client has not opened yet holds a default union, so it is read only once it has a state.</summary>
        private static void ReadMediaPlayer(World sceneWorld, Entity entity, ref AudioEntry entry)
        {
            if (!sceneWorld.TryGet(entity, out MediaPlayerComponent player))
                return;

            entry.State = FromVideoState(player.State);
            entry.IsLiveStream = entry.IsLiveStream || player.IsLiveStream;

            if (player.State == VideoState.VsNone)
                return;

            entry.Playing = player.IsPlaying;
            entry.CurrentTime = player.CurrentTime;
            entry.Duration = player.Duration;
            entry.Spatial = player.IsSpatial;
            entry.MinDistance = player.SpatialMinDistance;
            entry.MaxDistance = player.SpatialMaxDistance;

            AudioSource? audioSource = player.MediaPlayer.AnyExposedAudioSource();

            if (audioSource != null)
                entry.LiveVolume = audioSource.volume;
        }

        private static JToken Optional(float? value) =>
            value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
    }
}
