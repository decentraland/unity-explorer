using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Multiplayer.Profiles.Tables;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using LiveKit.Rooms.Streaming.Audio;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    /// <summary>
    /// Drives voice-driven lip sync animation for remote participants.
    /// Adds <see cref="ProximityLipSyncComponent"/> when a remote entity has both
    /// a <see cref="ProximityAudioSourceComponent"/> and a visible mouth renderer,
    /// then animates the mouth texture each frame based on audio amplitude or frequency bands.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProximityAudioPositionSystem))]
    public partial class ProximityLipSyncSystem : BaseUnityLoopSystem
    {
        private static readonly int MOUTH_TEX_ARR = Shader.PropertyToID("_MainTexArr");
        private static readonly int MOUTH_TEX_ARR_ID = Shader.PropertyToID("_MainTexArr_ID");

        // Amplitude-weighted groups
        //   SLIGHT: barely open (5=small oo, 7=small o, 11=tiny oval, 15=narrow smirk)
        //   MEDIUM: mid-open  (1=round O, 3=teeth+tongue, 8=oval+tongue, 9=oval+tongue, 14=round O)
        //   WIDE:   wide open (0=teeth grimace, 4=AH+tongue, 6=smile+tongue, 10=teeth grid, 12=grin, 13=big open)
        private static readonly int[] POSES_SLIGHT = { 5, 7, 11, 15 };
        private static readonly int[] POSES_MEDIUM = { 1, 3, 8, 9, 14 };
        private static readonly int[] POSES_WIDE = { 0, 4, 6, 10, 12, 13 };

        // Frequency-band groups — matched to actual Mouth_Atlas.png sprites
        //   OPEN_VOWEL:   mouth wide, tongue visible — A, O sounds  (4, 6, 8, 9, 13)
        //   CLOSED_VOWEL: rounded/small — O, OO, E sounds           (1, 5, 7, 11, 14)
        //   SIBILANT:     teeth visible, narrow/grit — S, SH, F     (0, 3, 10, 12, 15)
        private static readonly int[] POSES_OPEN_VOWEL = { 4, 6, 8, 9, 13 };
        private static readonly int[] POSES_CLOSED_VOWEL = { 1, 5, 7, 11, 14 };
        private static readonly int[] POSES_SIBILANT = { 0, 3, 10, 12, 15 };

        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly ConcurrentDictionary<string, AudioSource> activeAudioSources;
        private readonly ProximityConfigHolder configHolder;
        private readonly List<Entity> entitiesToCleanUp = new ();
        private readonly MaterialPropertyBlock lipSyncPropertyBlock = new ();

        internal ProximityLipSyncSystem(
            World world,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            ConcurrentDictionary<string, AudioSource> activeAudioSources,
            ProximityConfigHolder configHolder) : base(world)
        {
            this.entityParticipantTable = entityParticipantTable;
            this.activeAudioSources = activeAudioSources;
            this.configHolder = configHolder;
        }

        protected override void Update(float t)
        {
            if (configHolder.Config == null) return;

            SetupPendingLipSync();

            if (configHolder.MouthTextureArray != null)
                UpdateLipSyncQuery(World, t);

            CleanUpOrphanedLipSyncQuery(World);
            ProcessCleanUp();
        }

        /// <summary>
        /// Iterates <see cref="activeAudioSources"/> and adds <see cref="ProximityLipSyncComponent"/>
        /// to entities that have a proximity audio source and a visible mouth renderer but no lip sync yet.
        /// </summary>
        private void SetupPendingLipSync()
        {
            if (configHolder.MouthTextureArray == null) return;

            int idlePose = configHolder.Config!.LipSyncIdlePoseIndex;

            foreach (KeyValuePair<string, AudioSource> kvp in activeAudioSources)
            {
                if (!entityParticipantTable.TryGet(kvp.Key, out IReadOnlyEntityParticipantTable.Entry entry))
                    continue;

                Entity entity = entry.Entity;

                if (World.Has<DeleteEntityIntention>(entity)) continue;
                if (World.Has<ProximityLipSyncComponent>(entity)) continue;

                Renderer mouthRenderer = FindMouthRendererForEntity(entity);
                if (mouthRenderer == null) continue;

                LivekitAudioSource lka = kvp.Value != null ? kvp.Value.GetComponent<LivekitAudioSource>() : null;

                World.Add(entity, new ProximityLipSyncComponent
                {
                    ParticipantIdentity = kvp.Key,
                    MouthRenderer = mouthRenderer,
                    LivekitSource = lka,
                    CurrentPoseIndex = idlePose,
                    PoseHoldTimer = 0f,
                    SmoothedAmplitude = 0f,
                });
            }
        }

        /// <summary>
        /// Source-generated query: lip sync animation with switchable modes.
        /// <see cref="LipSyncMode.AmplitudeWeighted"/>: full-spectrum RMS -> pose group by loudness.
        /// <see cref="LipSyncMode.SpeechBandAmplitude"/>: bandpass-filtered RMS -> same grouping.
        /// <see cref="LipSyncMode.FrequencyBands"/>: Goertzel band energies -> vowel/consonant/sibilant.
        /// </summary>
        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateLipSync(
            [Data] float dt,
            ref ProximityLipSyncComponent lipSync,
            ref AvatarShapeComponent avatarShape)
        {
            if (lipSync.MouthRenderer == null)
            {
                Renderer mouthRenderer = FindMouthRenderer(in avatarShape);
                if (mouthRenderer == null) return;

                lipSync.MouthRenderer = mouthRenderer;
                lipSync.PoseHoldTimer = 0f;
            }

            Texture2DArray mouthTextures = configHolder.MouthTextureArray!;
            VoiceChatConfiguration config = configHolder.Config!;
            LivekitAudioSource src = lipSync.LivekitSource;

            if (src != null)
            {
                src.speechBandLowHz = config.LipSyncSpeechBandLowHz;
                src.speechBandHighHz = config.LipSyncSpeechBandHighHz;
            }

            float rawAmplitude = src != null
                ? config.LipSyncMode == LipSyncMode.SpeechBandAmplitude
                    ? src.LipSyncSpeechAmplitude
                    : src.LipSyncAmplitude
                : 0f;

            float smoothLerp = config.LipSyncSmoothingFactor * dt * 60f;
            float target = rawAmplitude * config.LipSyncAmplitudeSensitivity;
            lipSync.SmoothedAmplitude = Mathf.Lerp(lipSync.SmoothedAmplitude, target, smoothLerp);

            int idlePose = config.LipSyncIdlePoseIndex;

            if (lipSync.SmoothedAmplitude < config.LipSyncSilenceThreshold)
            {
                lipSync.CurrentPoseIndex = idlePose;
                lipSync.PoseHoldTimer = 0f;
            }
            else
            {
                lipSync.PoseHoldTimer -= dt;

                if (lipSync.PoseHoldTimer <= 0f)
                {
                    int pose;

                    if (config.LipSyncMode == LipSyncMode.FrequencyBands && src != null)
                    {
                        pose = SelectPoseByBands(src, config.LipSyncBandSensitivity,
                            config.LipSyncSpectralPeakedness, idlePose);
                    }
                    else
                    {
                        pose = SelectPoseByAmplitude(lipSync.SmoothedAmplitude);
                    }

                    lipSync.CurrentPoseIndex = pose;
                    lipSync.PoseHoldTimer = config.LipSyncPoseHoldDuration;
                }
            }

            lipSyncPropertyBlock.SetFloat(MOUTH_TEX_ARR_ID, lipSync.CurrentPoseIndex);
            lipSyncPropertyBlock.SetTexture(MOUTH_TEX_ARR, mouthTextures);
            lipSync.MouthRenderer.SetPropertyBlock(lipSyncPropertyBlock);
        }

        /// <summary>
        /// Collects entities that lost their <see cref="ProximityAudioSourceComponent"/>
        /// but still have <see cref="ProximityLipSyncComponent"/> for deferred removal.
        /// </summary>
        [Query]
        [None(typeof(ProximityAudioSourceComponent), typeof(DeleteEntityIntention))]
        private void CleanUpOrphanedLipSync(Entity entity, ref ProximityLipSyncComponent lipSync)
        {
            ClearMouthRenderer(ref lipSync);
            entitiesToCleanUp.Add(entity);
        }

        private void ClearMouthRenderer(ref ProximityLipSyncComponent lipSync)
        {
            if (lipSync.MouthRenderer != null)
            {
                lipSyncPropertyBlock.Clear();
                lipSync.MouthRenderer.SetPropertyBlock(lipSyncPropertyBlock);
            }
        }

        private void ProcessCleanUp()
        {
            foreach (Entity entity in entitiesToCleanUp)
            {
                if (World.Has<ProximityLipSyncComponent>(entity))
                    World.Remove<ProximityLipSyncComponent>(entity);
            }

            entitiesToCleanUp.Clear();
        }

        private static int SelectPoseByAmplitude(float smoothed)
        {
            int[] group = smoothed < 0.15f ? POSES_SLIGHT
                        : smoothed < 0.45f ? POSES_MEDIUM
                        : POSES_WIDE;
            return group[Random.Range(0, group.Length)];
        }

        /// <summary>
        /// Selects a pose based on Goertzel band energies.
        /// Returns <paramref name="idlePose"/> if the signal looks like music
        /// (energy too evenly spread across bands) rather than speech
        /// (energy concentrated in 1-2 bands).
        /// </summary>
        private static int SelectPoseByBands(
            LivekitAudioSource src, float sensitivity, float peakednessThreshold, int idlePose)
        {
            float low = src.LipSyncBandLow * sensitivity;
            float mid = src.LipSyncBandMid * sensitivity;
            float high = src.LipSyncBandHigh * sensitivity;

            float total = low + mid + high;
            if (total < 0.001f) return idlePose;

            float maxBand = Mathf.Max(low, Mathf.Max(mid, high));

            if (maxBand / total < peakednessThreshold) return idlePose;

            int[] group;

            if (high > low && high > mid)
                group = POSES_SIBILANT;
            else if (low > mid * 1.3f)
                group = POSES_OPEN_VOWEL;
            else
                group = POSES_CLOSED_VOWEL;

            return group[Random.Range(0, group.Length)];
        }

        private Renderer FindMouthRendererForEntity(Entity entity)
        {
            ref readonly AvatarShapeComponent avatarShape =
                ref World.TryGetRef<AvatarShapeComponent>(entity, out bool hasAvatar);

            if (!hasAvatar) return null;

            return FindMouthRenderer(in avatarShape);
        }

        private static Renderer FindMouthRenderer(in AvatarShapeComponent avatarShape)
        {
            for (int i = 0; i < avatarShape.InstantiatedWearables.Count; i++)
            {
                List<Renderer> renderers = avatarShape.InstantiatedWearables[i].Renderers;

                for (int j = 0; j < renderers.Count; j++)
                {
                    if (renderers[j] != null && renderers[j].name.EndsWith("Mask_Mouth"))
                        return renderers[j];
                }
            }

            return null;
        }
    }
}
