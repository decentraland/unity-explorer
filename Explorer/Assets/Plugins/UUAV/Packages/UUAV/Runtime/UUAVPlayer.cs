using System;
using System.Threading;
using UnityEngine;

namespace UUAV
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class UUAVPlayer : MonoBehaviour
    {
        private delegate ResultFFI TimeQuery(ulong playerId, out double value);

        // Cache to avoid per-call allocs
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

            // e.g. "h264"
            public string VideoCodec;

            // e.g. "yuv420p"
            public string PixelFormat;

            // e.g. "aac"
            public string AudioCodec;

            // e.g. "fltp"
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

        [SerializeField] private string url = "";
        [SerializeField] private bool playOnStart;

        // optional user-provided surface; auto-allocated at video size when null
        [SerializeField] private RenderTexture? targetTexture;

        [Header("Debug View")]
        // immutable after Awake; 0 means creation failed and the component is inert
        [SerializeField]
        private ulong playerId;

        [SerializeField] private int nativeChannels;
        [SerializeField] private VideoSize videoSize;

        [SerializeField] private bool enableDebugGather;
        [SerializeField] private double currentTimeDebug;
        [SerializeField] private double durationDebug;
        [SerializeField] private string? nativeState;
        [SerializeField] private MediaInfo_M mediaInfo;

        [Header("Resources")] [SerializeField] private Material? nv12Material;
        [SerializeField] private AudioSource audioSource = null!;
        [SerializeField] private Texture2D? yPlane;
        [SerializeField] private Texture2D? uvPlane;
        [SerializeField] private RenderTexture? runtimeSurface;
        [SerializeField] private IntPtr nativeTexture;

        private static ulong playerIncrementalID;

        // written on the audio mixer thread via Interlocked, read on the
        // main thread through CopyDspStats
        private long dspFramesRequested;
        private long dspFramesReturned;
        private long dspSilencedCallbacks;

        public string CurrentUrl => url;

        // 0 = the runtime never negotiated an output format: every DSP
        // callback silences and the player is permanently mute
        public int NativeChannels => nativeChannels;

        public UUAVState State => NativeMethods.uuav_player_state(playerId);

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
            url = mediaUrl;
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

        // NOTE the native side already slaves the media clock to actual
        // audio consumption (helper-side AudioFeedback pacing); this hook
        // remains for callers that need a different external master
        public void AssignMasterClock(double mediaTime)
        {
            Check(
                NativeMethods.uuav_player_assign_master_clock(playerId, mediaTime),
                "assign master clock"
            );
        }

        public static UUAVPlayer New()
        {
            ulong currentID = ++playerIncrementalID;
            GameObject gm = new GameObject($"UUAVPlayer_Instance_{currentID}");
            UUAVPlayer instance = gm.AddComponent<UUAVPlayer>();
            return instance;
        }

        internal ulong PlayerId => playerId;

        private void Awake()
        {
            // registered before the early returns below: an instance whose
            // native player failed to create still shows up in diagnostics
            UUAVDebug.Register(this);

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
                nativeState = NativeMethods.uuav_player_state(playerId).ToStringNoAlloc();

                if (TryGetMediaInfo(out MediaInfo m))
                {
                    mediaInfo = MediaInfo_M.From(m);
                }
            }

            switch (NativeMethods.uuav_player_state(playerId))
            {
                case UUAVState.Ready:
                case UUAVState.Playing:
                case UUAVState.Paused:
                case UUAVState.Ended:
                    break;
                default:
                    return;
            }

            // presents the due frame into the native NV12 texture on the render
            // thread; the blit below is enqueued after it in submission order,
            // so it samples the freshly presented frame in the same frame
            GL.IssuePluginEvent(UUAVRuntime.RenderCallback, (int)playerId);

            if (RefreshVideoTexture())
            {
                BlitToSurface();
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
            UUAVDebug.Unregister(this);

            audioSource.Stop();

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


        // audio mixer thread. playerId is immutable, safe to read directly;
        // a stale id after uuav_player_free is a native no-op returning 0
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (playerId == 0 || channels != nativeChannels)
            {
                // a channel mismatch means the engine's audio config changed;
                // renegotiation is runtime-wide, not per player,
                // emit silence rather than misinterleaved audio
                Array.Clear(data, 0, data.Length);
                Interlocked.Increment(ref dspSilencedCallbacks);
                return;
            }

            int frames = data.Length / channels;
            var read = NativeMethods.uuav_player_read_audio(playerId, data, frames);
            Interlocked.Add(ref dspFramesRequested, frames);
            Interlocked.Add(ref dspFramesReturned, read);
            if (read == 0)
            {
                // missing/freed player leaves the buffer untouched
                Array.Clear(data, 0, data.Length);
            }
        }

        // main-thread view of the audio-thread counters; cumulative for the
        // component's lifetime
        public void CopyDspStats(out long framesRequested, out long framesReturned, out long silencedCallbacks)
        {
            framesRequested = Interlocked.Read(ref dspFramesRequested);
            framesReturned = Interlocked.Read(ref dspFramesReturned);
            silencedCallbacks = Interlocked.Read(ref dspSilencedCallbacks);
        }

        private bool RefreshVideoTexture()
        {
            var textureResult = NativeMethods.uuav_player_get_video_texture(
                playerId,
                0,
                out var yTexture
            );
            if (textureResult.IsOk == false)
            {
                // expected until the first frame is presented
                textureResult.ConsumeError();
                return false;
            }

            var uvTexture = yTexture;

            // on Metal each plane is its own texture; both pointers change
            // together on resolution change, so polling plane 0 covers both.
            // on D3D11 native ignores the plane and returns the one resource
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            var uvResult = NativeMethods.uuav_player_get_video_texture(
                    playerId,
                    1,
                    out uvTexture
                    );
            if (uvResult.IsOk == false)
            {
                uvResult.ConsumeError();
                return false;
            }
#endif

            var sizeResult = NativeMethods.uuav_player_get_video_size(playerId, out var size);
            if (sizeResult.IsOk == false)
            {
                sizeResult.ConsumeError();
                return false;
            }

            // the native texture is recreated on every resolution change,
            // including mid-play adaptive/IDR switches, so pointer + size
            // comparison catches first frame, re-open and mid-stream changes
            if (
                yTexture != nativeTexture
                || size.Width != videoSize.Width
                || size.Height != videoSize.Height
            )
            {
                RecreatePlaneViews(yTexture, uvTexture, size);
            }

            return yPlane != null;
        }

        private void RecreatePlaneViews(IntPtr yTexture, IntPtr uvTexture, VideoSize size)
        {
            ReleasePlaneViews();

            var width = (int)size.Width;
            var height = (int)size.Height;

            // D3D11: one NV12 resource passed for both planes, Unity selects
            // the plane from the view format (R8 -> Y, R8G8 -> UV).
            // Metal: two distinct MTLTextures, wrapped as-is
            yPlane = Texture2D.CreateExternalTexture(
                width,
                height,
                TextureFormat.R8,
                mipChain: false,
                linear: true,
                yTexture
            );
            uvPlane = Texture2D.CreateExternalTexture(
                width / 2,
                height / 2,
                TextureFormat.RG16,
                mipChain: false,
                linear: true,
                uvTexture
            );
            ConfigurePlane(yPlane);
            ConfigurePlane(uvPlane);

            if (targetTexture == null)
            {
                if (runtimeSurface != null)
                {
                    runtimeSurface.Release();
                    Destroy(runtimeSurface);
                }

                // BGRA8 group so the surface is Graphics.CopyTexture-compatible with
                // BGRA render targets (the D3D11 output format of most video pipelines).
                // Default read/write leaves the surface sRGB-flagged in Linear projects;
                // CopyTexture destinations must be sRGB-flagged too, or the raw bit copy
                // transplants gamma bytes that get sampled as linear (too bright)
                runtimeSurface = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32);
            }

            nativeTexture = yTexture;
            videoSize = size;
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

            // script-context blits inherit whatever sRGB-write state the last
            // render left behind; without the encode the shader's linear output
            // lands raw in the sRGB surface and decodes too dark
            bool previousSRGBWrite = GL.sRGBWrite;
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(null, surface, nv12Material);
            GL.sRGBWrite = previousSRGBWrite;
        }

        private void ReleasePlaneViews()
        {
            // wrappers only: Unity drops its SRVs, the native resource is untouched
            if (yPlane != null)
            {
                Destroy(yPlane);
                yPlane = null;
            }

            if (uvPlane != null)
            {
                Destroy(uvPlane);
                uvPlane = null;
            }

            nativeTexture = IntPtr.Zero;
            videoSize = default;
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

            // unavailable is expected (no media open / realtime stream)
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
