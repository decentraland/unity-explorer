// SPDX-License-Identifier: Apache-2.0

using System;
using UnityEngine;
using UnitedAV.Internal;

namespace UnitedAV
{
    [AddComponentMenu("UnitedAV/Media Player")]
    public partial class MediaPlayer : MonoBehaviour
    {
        private IntPtr _native = IntPtr.Zero;

        private MediaControlImpl _control;
        private MediaInfoImpl _info;
        private TextureProducerImpl _textureProducer;

        [SerializeField] private MediaPlayerEvent _events = new MediaPlayerEvent();

        [SerializeField] private bool _autoOpen = false;
        [SerializeField, Range(0f, 1f)] private float _audioVolume = 1f;
        [SerializeField] private string _mediaPath = string.Empty;
        [SerializeField] private MediaPathType _mediaPathType = MediaPathType.AbsolutePathOrURL;

        [SerializeField] private PlatformOptionsWindows _platformOptionsWindows = new PlatformOptionsWindows();
        [SerializeField] private PlatformOptions_macOS _platformOptions_macOS = new PlatformOptions_macOS();

        [SerializeField] private AudioSource _audioSource;

        private bool _mediaOpened;
        private UAVState _lastState = UAVState.Idle;
        private bool _firstFrameFired;
        private bool _metaDataFired;
        private bool _startedFired;
        private bool _wasSeeking;
        private bool _wasBuffering;
        private ErrorCode _lastReportedError = ErrorCode.None;

        public IMediaControl Control => _control;
        public IMediaInfo Info => _info;
        public ITextureProducer TextureProducer => _textureProducer;
        public MediaPlayerEvent Events => _events;

        public float AudioVolume
        {
            get => _audioVolume;
            set
            {
                _audioVolume = Mathf.Clamp01(value);
                if (_native != IntPtr.Zero)
                    UnitedAVNative.uav_set_volume(_native, _audioVolume);
                if (_audioSource != null)
                    _audioSource.volume = _audioVolume;
            }
        }

        public AudioSource AudioSource
        {
            get => _audioSource;
            set => SetAudioSource(value);
        }

        public bool AutoOpen
        {
            get => _autoOpen;
            set => _autoOpen = value;
        }

        public PlatformOptionsWindows PlatformOptionsWindows
        {
            get => _platformOptionsWindows;
            set => _platformOptionsWindows = value;
        }

        public PlatformOptions_macOS PlatformOptions_macOS
        {
            get => _platformOptions_macOS;
            set => _platformOptions_macOS = value;
        }

        public bool MediaOpened => _mediaOpened;

        internal IntPtr NativeHandle => _native;

        private void Awake()
        {
            EnsureNativeCreated();

            _control = new MediaControlImpl(this);
            _info = new MediaInfoImpl(this);
            _textureProducer = new TextureProducerImpl(this);

            if (_audioSource == null)
                TryGetComponent(out _audioSource);
        }

        private void Start()
        {
            if (_autoOpen && !string.IsNullOrEmpty(_mediaPath))
                OpenMedia(_mediaPathType, _mediaPath, autoPlay: true);
        }

        private void Update()
        {
            if (_native == IntPtr.Zero)
                return;

            PollStateAndFireEvents();
            UpdateVideoTexture();
        }

        private void OnDestroy()
        {
            TeardownAudio();
            _textureProducer?.Dispose();

            if (_native != IntPtr.Zero)
            {
                UnitedAVNative.uav_destroy(_native);
                _native = IntPtr.Zero;
            }
        }

        private void EnsureNativeCreated()
        {
            if (_native == IntPtr.Zero)
            {
                _native = UnitedAVNative.uav_create();
                if (_native == IntPtr.Zero)
                {
                    Debug.LogError("[UnitedAV] uav_create() returned null; native plugin missing or failed to init.");
                    return;
                }

                UnitedAVNative.uav_set_volume(_native, _audioVolume);
            }
        }

        public bool OpenMedia(MediaPathType pathType, string path, bool autoPlay)
        {
            EnsureNativeCreated();
            if (_native == IntPtr.Zero || string.IsNullOrEmpty(path))
                return false;

            if (_mediaOpened)
                CloseMedia();

            _mediaPathType = pathType;
            _mediaPath = path;

            string resolved = ResolvePath(pathType, path);

            int rc = UnitedAVNative.uav_open(_native, resolved);
            if (rc != (int)UAVResult.Ok)
            {
                _lastReportedError = MapError(rc);
                FireEvent(MediaPlayerEvent.EventType.Error, _lastReportedError);
                return false;
            }

            _mediaOpened = true;
            _lastState = UAVState.Idle;
            _firstFrameFired = false;
            _metaDataFired = false;
            _startedFired = false;
            _wasSeeking = false;
            _wasBuffering = false;
            _lastReportedError = ErrorCode.None;

            UnitedAVNative.uav_set_volume(_native, _audioVolume);

            if (autoPlay)
                UnitedAVNative.uav_play(_native);

            return true;
        }

        public void Stop()
        {
            if (_native != IntPtr.Zero)
                UnitedAVNative.uav_stop(_native);
        }

        public void CloseMedia()
        {
            if (_native != IntPtr.Zero && _mediaOpened)
            {
                FireEvent(MediaPlayerEvent.EventType.Closing, ErrorCode.None);
                UnitedAVNative.uav_close(_native);
            }

            _mediaOpened = false;
            _lastState = UAVState.Idle;
            _firstFrameFired = false;
            _metaDataFired = false;
            _startedFired = false;
            _lastFrameId = -1;

            TeardownAudio();
            _textureProducer?.ClearTexture();
            DestroyTextureInternal();
        }

        public void SetAudioSource(AudioSource audioSource)
        {
            _audioSource = audioSource;
            if (_audioClip != null && _audioSource != null)
            {
                _audioSource.clip = _audioClip;
                _audioSource.loop = true;
                _audioSource.volume = _audioVolume;
                if (!_audioSource.isPlaying)
                    _audioSource.Play();
            }
        }

        private static string ResolvePath(MediaPathType pathType, string path)
        {
            switch (pathType)
            {
                case MediaPathType.AbsolutePathOrURL:
                    return path;

                case MediaPathType.RelativeToProjectFolder:
                    return CombineUnder(GetProjectFolder(), path);

                case MediaPathType.RelativeToStreamingAssetsFolder:
                    return CombineUnder(Application.streamingAssetsPath, path);

                case MediaPathType.RelativeToDataFolder:
                    return CombineUnder(Application.dataPath, path);

                case MediaPathType.RelativeToPersistentDataFolder:
                    return CombineUnder(Application.persistentDataPath, path);

                default:
                    return path;
            }
        }

        private static string GetProjectFolder()
        {
            string data = Application.dataPath;
            try
            {
                string parent = System.IO.Path.GetDirectoryName(data);
                return string.IsNullOrEmpty(parent) ? data : parent;
            }
            catch
            {
                return data;
            }
        }

        private static string CombineUnder(string root, string relative)
        {
            if (string.IsNullOrEmpty(root))
                return relative;
            return System.IO.Path.Combine(root, relative);
        }

        internal static ErrorCode MapError(int nativeResult)
        {
            switch ((UAVResult)nativeResult)
            {
                case UAVResult.Ok:
                    return ErrorCode.None;
                case UAVResult.ErrOpenFailed:
                case UAVResult.ErrNoStream:
                    return ErrorCode.LoadFailed;
                case UAVResult.ErrDecode:
                    return ErrorCode.DecodeFailed;
                case UAVResult.ErrUnsupported:
                    return ErrorCode.DecodeFailed;
                case UAVResult.ErrInvalid:
                    return ErrorCode.InvalidOperation;
                case UAVResult.ErrNoMem:
                    return ErrorCode.LoadFailed;
                default:
                    return nativeResult < 0 ? ErrorCode.LoadFailed : ErrorCode.None;
            }
        }

        private void FireEvent(MediaPlayerEvent.EventType et, ErrorCode code)
        {
            _events?.Invoke(this, et, code);
        }

        private void PollStateAndFireEvents()
        {
            if (!_mediaOpened)
                return;

            var state = (UAVState)UnitedAVNative.uav_get_state(_native);

            if (!_metaDataFired && state >= UAVState.Ready && state != UAVState.Error)
            {
                _metaDataFired = true;
                FireEvent(MediaPlayerEvent.EventType.MetaDataReady, ErrorCode.None);

                SetupAudioIfNeeded();

                FireEvent(MediaPlayerEvent.EventType.ReadyToPlay, ErrorCode.None);
            }

            bool seeking = state == UAVState.Buffering && _wasSeekRequested;
            if (seeking && !_wasSeeking)
                FireEvent(MediaPlayerEvent.EventType.StartedSeeking, ErrorCode.None);
            if (!seeking && _wasSeeking)
            {
                FireEvent(MediaPlayerEvent.EventType.FinishedSeeking, ErrorCode.None);
                _wasSeekRequested = false;
            }
            _wasSeeking = seeking;

            bool buffering = state == UAVState.Buffering && !_wasSeekRequested;
            if (buffering && !_wasBuffering)
            {
                FireEvent(MediaPlayerEvent.EventType.StartedBuffering, ErrorCode.None);
                FireEvent(MediaPlayerEvent.EventType.Stalled, ErrorCode.None);
            }
            if (!buffering && _wasBuffering)
            {
                FireEvent(MediaPlayerEvent.EventType.FinishedBuffering, ErrorCode.None);
                FireEvent(MediaPlayerEvent.EventType.Unstalled, ErrorCode.None);
            }
            _wasBuffering = buffering;

            if (state != _lastState)
            {
                switch (state)
                {
                    case UAVState.Playing:
                        if (!_startedFired)
                        {
                            FireEvent(MediaPlayerEvent.EventType.Started, ErrorCode.None);
                            _startedFired = true;
                        }
                        if (_lastState == UAVState.Paused)
                            FireEvent(MediaPlayerEvent.EventType.Unpaused, ErrorCode.None);
                        EnsureAudioPlaying();
                        break;

                    case UAVState.Paused:
                        FireEvent(MediaPlayerEvent.EventType.Paused, ErrorCode.None);
                        EnsureAudioPaused();
                        break;

                    case UAVState.Finished:
                        FireEvent(MediaPlayerEvent.EventType.FinishedPlaying, ErrorCode.None);
                        EnsureAudioPaused();
                        break;

                    case UAVState.Error:
                        _lastReportedError = MapError(UnitedAVNative.uav_last_error(_native));
                        if (_lastReportedError == ErrorCode.None)
                            _lastReportedError = ErrorCode.DecodeFailed;
                        FireEvent(MediaPlayerEvent.EventType.Error, _lastReportedError);
                        break;
                }

                _lastState = state;
            }
        }

        // Set by Seek() so the poll loop distinguishes seek-buffering from network stalls.
        private bool _wasSeekRequested;
        internal void NotifySeekRequested() => _wasSeekRequested = true;
    }
}
