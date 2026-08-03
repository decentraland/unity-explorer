using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvProSwitch;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using UUAV;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Fills the <see cref="MediaPlayerDebugRegistry" /> snapshot for the "Media Player"
    ///     debug widget. Passive by contract: while no collection is requested the update is
    ///     two boolean reads, and all row/string building happens only on an explicit request
    ///     from the widget (its Update button or Auto Update poll).
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateMediaPlayerSystem))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class GatherMediaStreamDebugSystem : BaseUnityLoopSystem
    {
        private readonly MediaPlayerDebugRegistry registry;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ISceneData sceneData;

        // per-collection aggregation, reused between requests
        private readonly List<(string name, string value)> rowsBuffer = new ();
        private int videoPlayerCount;
        private int audioStreamCount;

        internal GatherMediaStreamDebugSystem(
            World world,
            MediaPlayerDebugRegistry registry,
            ISceneStateProvider sceneStateProvider,
            ISceneData sceneData) : base(world)
        {
            this.registry = registry;
            this.sceneStateProvider = sceneStateProvider;
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            if (!registry.CollectRequested || !sceneStateProvider.IsCurrent)
                return;

            rowsBuffer.Clear();
            videoPlayerCount = 0;
            audioStreamCount = 0;

            GatherVideoPlayerQuery(World);
            GatherAudioStreamQuery(World);

            registry.Update(sceneData.SceneShortInfo.ToString(), videoPlayerCount, audioStreamCount, rowsBuffer, Time.frameCount);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void GatherVideoPlayer(Entity entity, ref MediaPlayerComponent component, PBVideoPlayer sdkComponent)
        {
            videoPlayerCount++;
            rowsBuffer.Add(($"#{entity.Id} Video", sdkComponent.Src));
            rowsBuffer.Add(("  sdk", $"playing:{Optional(sdkComponent.HasPlaying, sdkComponent.Playing)} vol:{Optional(sdkComponent.HasVolume, sdkComponent.Volume)} loop:{Optional(sdkComponent.HasLoop, sdkComponent.Loop)}"));
            AddSharedRows(ref component);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void GatherAudioStream(Entity entity, ref MediaPlayerComponent component, PBAudioStream sdkComponent)
        {
            audioStreamCount++;
            rowsBuffer.Add(($"#{entity.Id} Audio", sdkComponent.Url));
            rowsBuffer.Add(("  sdk", $"playing:{Optional(sdkComponent.HasPlaying, sdkComponent.Playing)} vol:{Optional(sdkComponent.HasVolume, sdkComponent.Volume)}"));
            AddSharedRows(ref component);
        }

        /// <summary>
        ///     Rows shared by every media entity, tracing MediaPlayerComponent state and the
        ///     backend underneath it. Read-only over the component: <c>State</c> is whatever
        ///     <see cref="UpdateMediaPlayerSystem" /> last wrote (never call
        ///     <c>UpdateState</c> here - it mutates frozen-tracking bookkeeping).
        /// </summary>
        private void AddSharedRows(ref MediaPlayerComponent component)
        {
            if (!component.MediaPlayer.IsValid)
            {
                rowsBuffer.Add(("  state", $"{component.State} (backend destroyed)"));
                return;
            }

            component.IsFrozen(out float frozenFor);

            rowsBuffer.Add(("  state", $"{component.State} [{BackendLabel(ref component)}]"));
            rowsBuffer.Add(("  time", $"{component.CurrentTime:F1}/{component.Duration:F1} live:{component.IsLiveStream} frozen:{frozenFor:F1}s"));
            rowsBuffer.Add(("  flags", $"failed:{component.HasFailed} spatial:{component.IsSpatial} err:{component.MediaPlayer.GetLastError()}"));

            Texture? texture = component.MediaPlayer.LastTexture();
            rowsBuffer.Add(("  texture", texture == null ? "none" : $"{texture.width}x{texture.height}"));

            AddUuavRow(ref component);
        }

        // The backend is chosen once globally at startup, so the per-player kind follows
        // from the address type plus the global switch.
        private static string BackendLabel(ref MediaPlayerComponent component) =>
            component.MediaPlayer.IsLivekitPlayer(out _) ? "LiveKit"
            : MediaPlayerBackendSelection.UseCustomPlayer ? "UUAV" : "AVPro";

        /// <summary>
        ///     Native-layer drill-down: UUAVPlayer lives on the same GameObject as the
        ///     AvProSwitch MediaPlayer when the UUAV backend is active. Comparing its state
        ///     with the ECS <c>State</c> row shows which layer a stall lives in.
        /// </summary>
        private void AddUuavRow(ref MediaPlayerComponent component)
        {
            if (!MediaPlayerBackendSelection.UseCustomPlayer)
                return;

            if (!component.MediaPlayer.TryGetAvProPlayer(out MediaPlayer? mediaPlayer) || mediaPlayer == null)
                return;

            UUAVPlayer? uuavPlayer = mediaPlayer.GetComponent<UUAVPlayer>();

            if (uuavPlayer == null)
            {
                rowsBuffer.Add(("  uuav", "no UUAVPlayer on backend GameObject"));
                return;
            }

            string detail = uuavPlayer.TryGetMediaInfo(out MediaInfo info)
                ? $"{uuavPlayer.State.ToStringNoAlloc()} {info.Width}x{info.Height} {info.VideoCodec} video:{info.HasVideo} audio:{info.HasAudio}"
                : uuavPlayer.State.ToStringNoAlloc();

            rowsBuffer.Add(("  uuav", detail));
        }

        private static string Optional<T>(bool has, T value) where T: struct =>
            has ? value.ToString()! : "unset";
    }
}
