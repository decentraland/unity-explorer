using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UUAV
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class UUAVPlayer : MonoBehaviour
    {
        private delegate ResultFFI TimeQuery(ulong playerId, out double value);

        private static readonly TimeQuery currentTimeQuery = NativeMethods.uuav_player_current_time;
        private static readonly TimeQuery durationQuery = NativeMethods.uuav_player_duration;

        /// <summary>
        /// M stands for "Managed" mirrors the original MediaInfo and makes it serializable
        /// </summary>
        [Serializable]
        public struct MediaInfo_M
        {
            public double Duration;
            public double Framerate;
            public long VideoBitrate;
            public long AudioBitrate;
            public uint Width;
            public uint Height;
            public int SampleRate;
            public int Channels;
            public bool HasVideo;

            public bool HasAudio;

            public string VideoCodec;

            public string PixelFormat;

            public string AudioCodec;

            public string SampleFormat;

            public static MediaInfo_M From(MediaInfo mediaInfo)
            {
                return new MediaInfo_M
                {
                    Duration = mediaInfo.Duration,
                    Framerate = mediaInfo.Framerate,
                    VideoBitrate = mediaInfo.VideoBitrate,
                    AudioBitrate = mediaInfo.AudioBitrate,
                    Width = mediaInfo.Width,
                    Height = mediaInfo.Height,
                    SampleRate = mediaInfo.SampleRate,
                    Channels = mediaInfo.Channels,
                    HasVideo = mediaInfo.HasVideo,
                    HasAudio = mediaInfo.HasAudio,
                    VideoCodec = mediaInfo.VideoCodec,
                    PixelFormat = mediaInfo.PixelFormat,
                    AudioCodec = mediaInfo.AudioCodec,
                    SampleFormat = mediaInfo.SampleFormat
                };
            }
        }

        private static readonly int YTexId = Shader.PropertyToID("_YTex");
        private static readonly int UVTexId = Shader.PropertyToID("_UVTex");

        private static readonly int[] YuvRowIds =
        {
            Shader.PropertyToID("_YuvR"),
            Shader.PropertyToID("_YuvG"),
            Shader.PropertyToID("_YuvB"),
        };

        private static readonly int[] UvRowIds =
        {
            Shader.PropertyToID("_UvX"),
            Shader.PropertyToID("_UvY"),
        };

        [SerializeField] private string url = "";
        [SerializeField] private bool playOnStart;

        [SerializeField] private RenderTexture? targetTexture;

        [Header("Debug View")]
        [SerializeField]
        private ulong playerId;

        [SerializeField] private int nativeChannels;

        [SerializeField] private bool enableDebugGather;
        [SerializeField] private double currentTimeDebug;
        [SerializeField] private double durationDebug;

        [SerializeField] private double audioClockDebug;
        [SerializeField] private double avOffsetDebug;
        [SerializeField] private string? nativeState;
        [SerializeField] private MediaInfo_M mediaInfo;

        [Header("Resources")] [SerializeField] private Material? nv12Material;
        [SerializeField] private AudioSource audioSource = null!;
        [SerializeField] private Texture2D? yPlane;
        [SerializeField] private Texture2D? uvPlane;
        [SerializeField] private RenderTexture? runtimeSurface;

        private readonly Dictionary<(IntPtr Texture, int Plane), Texture2D> planeViews = new();

        private FrameInfo frameInfo;
        private ulong drawnFrame;
        private ulong wrappedGeneration;

        private long audioFramesConsumed;
        private ulong masterClockGeneration;
        private double masterClockBasePts;
        private long masterClockBaseFrames;
        private int masterClockSampleRate;
        private bool masterClockActive;
        private float avSyncLogAt;

        private static bool audioSyncUnavailable;

        private static ulong playerIncrementalID;

        public string CurrentUrl => url;

        public UUAVState State => NativeMethods.uuav_player_state(playerId);

        public UUAVError LastError
        {
            get
            {
                ulong code = NativeMethods.uuav_player_get_last_error(playerId);

                return code switch
                {
                    0 => UUAVError.None,
                    (ulong)UUAVError.DecodeFailed => UUAVError.DecodeFailed,
                    _ => UUAVError.OpenFailed,
                };
            }
        }

        public double CurrentTime => ReadTime(currentTimeQuery);

        public double Duration => ReadTime(durationQuery);

        public bool Looping
        {
            get => NativeMethods.uuav_player_get_looping(playerId);
            set => Check(NativeMethods.uuav_player_set_looping(playerId, value), "set looping");
        }

        public double PlaybackRate
        {
            get => NativeMethods.uuav_player_get_rate(playerId);
            set => Check(NativeMethods.uuav_player_set_rate(playerId, value), "set rate");
        }

        public RenderTexture? CurrentTexture =>
            targetTexture != null ? targetTexture : runtimeSurface;

        public AudioSource AudioSource => audioSource;

        public void OpenMedia(string mediaUrl)
        {
            if (url.Length > 0)
            {
                UUAVFetchService.CancelUrl(url);
            }

            url = mediaUrl;
            ReleasePlaneViews();
            masterClockActive = false;
            Check(NativeMethods.uuav_player_open_media_async(playerId, mediaUrl), "open media");
        }

        [ContextMenu(nameof(PrintState))]
        private void PrintState()
        {
            Debug.Log($"[UUAV] state: {NativeMethods.uuav_player_state(playerId).ToStringNoAlloc()}");
        }

        [ContextMenu(nameof(PrintControlsState))]
        private void PrintControlsState()
        {
            var result = NativeMethods.uuav_player_current_controls_state(playerId, out var controls);
            if (result.IsOk == false)
            {
                Debug.LogError($"[UUAV] controls state: {result.ConsumeError()}");
                return;
            }

            Debug.Log(
                $"[UUAV] controls: play={controls.Play} (pending={controls.PlayPending}) "
                + $"looping={controls.Looping} (pending={controls.LoopingPending}) "
                + $"rate={controls.Rate} (pending={controls.RatePending})"
            );
        }

        [ContextMenu(nameof(OpenMedia))]
        private void OpenMedia()
        {
            OpenMedia(url);
        }

        [ContextMenu(nameof(CloseMedia))]
        public void CloseMedia()
        {
            if (url.Length > 0)
            {
                UUAVFetchService.CancelUrl(url);
            }

            ReleasePlaneViews();
            masterClockActive = false;
            Check(NativeMethods.uuav_player_close_media(playerId), "close media");
        }

        [ContextMenu(nameof(Play))]
        public void Play()
        {
            Check(NativeMethods.uuav_player_play(playerId), "play");
        }

        [ContextMenu(nameof(Pause))]
        public void Pause()
        {
            Check(NativeMethods.uuav_player_pause(playerId), "pause");
        }

        public void Seek(double time)
        {
            masterClockActive = false;
            Check(NativeMethods.uuav_player_seek_async(playerId, time), "seek");
        }

        public bool TryGetMediaInfo(out MediaInfo info)
        {
            info = default;
            if (playerId == 0)
            {
                return false;
            }

            if (State is UUAVState.Closed or UUAVState.Error)
            {
                return false;
            }

            var result = NativeMethods.uuav_player_get_media_info(playerId, out info);
            Check(result, "uuav_player_get_media_info");
            return result.IsOk;
        }

        public static UUAVPlayer New()
        {
            ulong currentID = ++playerIncrementalID;
            GameObject gm = new GameObject($"UUAVPlayer_Instance_{currentID}");
            UUAVPlayer instance = gm.AddComponent<UUAVPlayer>();
            return instance;
        }

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();

            var shader = Shader.Find("Hidden/UUAV/NV12ToRGB");
            if (shader == null)
            {
                Debug.LogError("[UUAV] shader Hidden/UUAV/NV12ToRGB not found");
                return;
            }

            nv12Material = new Material(shader);

            var newPlayer = NativeMethods.uuav_player_new();
            if (newPlayer.IsOk == false)
            {
                Debug.LogError($"[UUAV] new player: {newPlayer.ConsumeError()}");
                return;
            }

            playerId = newPlayer.PlayerId;
            nativeChannels = NativeMethods.uuav_status().AudioOptions.Channels;

            audioSource.Play();
        }

        private void Start()
        {
            if (playOnStart && url.Length > 0)
            {
                OpenMedia(url);
                Play();
            }
        }

        private void Update()
        {
            if (playerId == 0)
            {
                return;
            }

            if (enableDebugGather)
            {
                durationDebug = Duration;
                currentTimeDebug = CurrentTime;
                avOffsetDebug = currentTimeDebug - audioClockDebug;
                nativeState = NativeMethods.uuav_player_state(playerId).ToStringNoAlloc();

                if (TryGetMediaInfo(out MediaInfo m))
                {
                    mediaInfo = MediaInfo_M.From(m);
                }
            }

            var state = NativeMethods.uuav_player_state(playerId);
            switch (state)
            {
                case UUAVState.Ready:
                case UUAVState.Playing:
                case UUAVState.Paused:
                case UUAVState.Ended:
                    break;
                default:
                    return;
            }

            if (state == UUAVState.Playing)
            {
                FeedMasterClock();
            }

            GL.IssuePluginEvent(UUAVRuntime.RenderCallback, (int)playerId);

            if (RefreshVideoTexture() && frameInfo.FrameIndex != drawnFrame)
            {
                BlitToSurface();
                drawnFrame = frameInfo.FrameIndex;
            }
        }

        private void OnDisable()
        {
            if (State == UUAVState.Playing)
            {
                Pause();
            }
        }

        private void OnDestroy()
        {
            audioSource.Stop();

            if (url.Length > 0)
            {
                UUAVFetchService.CancelUrl(url);
            }

            if (playerId != 0)
            {
                NativeMethods.uuav_player_free(playerId);
            }

            ReleasePlaneViews();

            if (runtimeSurface != null)
            {
                runtimeSurface.Release();
                Destroy(runtimeSurface);
                runtimeSurface = null;
            }

            if (nv12Material != null)
            {
                Destroy(nv12Material);
                nv12Material = null;
            }
        }


        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (playerId == 0 || channels != nativeChannels)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            var read = NativeMethods.uuav_player_read_audio(playerId, data, data.Length / channels);
            if (read == 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            Interlocked.Add(ref audioFramesConsumed, read);
        }

        /// <summary>
        /// [main thread] Feeds the audio-consumed clock to the native side as
        /// the master: media time = basis pts + frames consumed since the
        /// basis, at the transport rate. Holds off while no basis belongs to
        /// the live ring generation - during open/seek/loop-wrap priming -
        /// which is exactly when a stale value would drag the new stream's
        /// clock back to the old stream's time.
        /// </summary>
        private void FeedMasterClock()
        {
            if (audioSyncUnavailable)
            {
                return;
            }

            AudioSync sync;
            try
            {
                if (NativeMethods.uuav_player_audio_sync(playerId, out sync) == 0)
                {
                    masterClockActive = false;
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
                audioSyncUnavailable = true;
                Debug.LogWarning("[UUAV] uuav_player_audio_sync is missing; audio-master clock disabled");
                return;
            }

            if (sync.Priming != 0 || sync.HasBasis == 0)
            {
                masterClockActive = false;
                return;
            }

            if (!masterClockActive || sync.Generation != masterClockGeneration)
            {
                masterClockGeneration = sync.Generation;
                masterClockBasePts = sync.BasePts;
                masterClockBaseFrames = (long)sync.BaseFrames;
                masterClockSampleRate = (int)sync.SampleRate;
                masterClockActive = true;
            }

            double mediaTime = AudioMasterClock.MediaTime(
                masterClockBasePts,
                Interlocked.Read(ref audioFramesConsumed),
                masterClockBaseFrames,
                sync.Rate,
                masterClockSampleRate
            );
            audioClockDebug = mediaTime;
            NativeMethods.uuav_player_set_presentation_clock(playerId, mediaTime);

            if (Time.unscaledTime >= avSyncLogAt)
            {
                avSyncLogAt = Time.unscaledTime + 2f;
                double transport = CurrentTime;
                Debug.Log(
                    $"[UUAV] avsync player={playerId} audio={mediaTime:F3} transport={transport:F3} "
                    + $"residual_ms={(transport - mediaTime) * 1000.0:F1} "
                    + $"frames={Interlocked.Read(ref audioFramesConsumed)} gen={sync.Generation} sr={masterClockSampleRate}");
            }
        }

        private bool RefreshVideoTexture()
        {
            var infoResult = NativeMethods.uuav_player_get_frame_info(playerId, out var info);
            if (infoResult.IsOk == false)
            {
                infoResult.ConsumeError();
                return false;
            }

            if (info.SurfaceGeneration != wrappedGeneration)
            {
                ReleasePlaneViews();
                wrappedGeneration = info.SurfaceGeneration;
            }

            frameInfo = info;
            yPlane = PlaneView(0, info);
            uvPlane = PlaneView(1, info);
            EnsureSurface(info);
            return yPlane != null && uvPlane != null;
        }

        /// <summary>
        /// The external-texture wrapper for one published plane, created on first
        /// sight of that native pointer and reused afterwards.
        /// </summary>
        private Texture2D? PlaneView(int plane, in FrameInfo info)
        {
            var native = info.Plane(plane);
            if (native == IntPtr.Zero)
            {
                return null;
            }

            var key = (native, plane);
            if (planeViews.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var size = info.PlaneSize(plane);
            var format = plane == 0
                ? info.BitDepth > 8 ? TextureFormat.R16 : TextureFormat.R8
                : info.BitDepth > 8 ? TextureFormat.RG32 : TextureFormat.RG16;
            var view = Texture2D.CreateExternalTexture(
                size.x,
                size.y,
                format,
                mipChain: false,
                linear: true,
                native
            );
            ConfigurePlane(view);
            planeViews[key] = view;
            return view;
        }

        /// <summary>
        /// Keeps the auto-allocated surface at the displayed size, which is
        /// transposed for a quarter-turn rotation.
        /// </summary>
        private void EnsureSurface(in FrameInfo info)
        {
            if (targetTexture != null)
            {
                return;
            }

            var quarter = info.IsRotatedQuarterTurn;
            var width = (int)(quarter ? info.VisibleHeight : info.VisibleWidth);
            var height = (int)(quarter ? info.VisibleWidth : info.VisibleHeight);
            if (runtimeSurface != null
                && runtimeSurface.width == width
                && runtimeSurface.height == height)
            {
                return;
            }

            if (runtimeSurface != null)
            {
                runtimeSurface.Release();
                Destroy(runtimeSurface);
            }

            runtimeSurface = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32);
        }

        private void BlitToSurface()
        {
            var surface = CurrentTexture;
            if (surface == null || nv12Material == null)
            {
                return;
            }

            nv12Material.SetTexture(YTexId, yPlane);
            nv12Material.SetTexture(UVTexId, uvPlane);
            for (var row = 0; row < YuvRowIds.Length; row++)
            {
                nv12Material.SetVector(YuvRowIds[row], frameInfo.YuvRow(row));
            }

            for (var row = 0; row < UvRowIds.Length; row++)
            {
                nv12Material.SetVector(UvRowIds[row], frameInfo.UvRow(row));
            }

            bool previousSRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(null, surface, nv12Material);
            GL.sRGBWrite = previousSRGBWrite;
        }

        /// <summary>
        /// The wrappers are views only: Unity drops its SRVs, the native resource
        /// is untouched.
        /// </summary>
        private void ReleasePlaneViews()
        {
            foreach (var view in planeViews.Values)
            {
                Destroy(view);
            }

            planeViews.Clear();
            yPlane = null;
            uvPlane = null;
            wrappedGeneration = 0;
            drawnFrame = 0;
        }

        private double ReadTime(TimeQuery query)
        {
            if (playerId == 0)
            {
                return 0;
            }

            var result = query(playerId, out var value);
            if (result.IsOk)
            {
                return value;
            }

            result.ConsumeError();
            return 0;
        }

        private static void Check(ResultFFI result, string operation)
        {
            if (result.IsOk == false)
            {
                Debug.LogError($"[UUAV] {operation}: {result.ConsumeError()}");
            }
        }

        private static void ConfigurePlane(Texture2D plane)
        {
            plane.filterMode = FilterMode.Bilinear;
            plane.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
