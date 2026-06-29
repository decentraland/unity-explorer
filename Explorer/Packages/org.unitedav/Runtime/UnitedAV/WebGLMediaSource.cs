// SPDX-License-Identifier: Apache-2.0
// Unity WebGL media source: the browser decodes the video (HTMLVideoElement) and
// each Tick() uploads the current frame into a Texture2D's underlying GL texture
// via the jslib. Implements the same IMediaControl/IMediaInfo/ITextureProducer
// surface as the native player so consumer code is unchanged on WebGL.
//
// MediaPlayer can select this backend under `#if UNITY_WEBGL && !UNITY_EDITOR`.
// The browser handles audio itself (HTMLVideoElement); there is no PCM tap.
#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;
using UnitedAV.Internal;

namespace UnitedAV
{
    public sealed class WebGLMediaSource : IMediaControl, IMediaInfo, ITextureProducer
    {
        int _h;
        Texture2D _tex;
        int _w, _hgt;
        bool _looping;
        float _rate = 1f;
        ErrorCode _err = ErrorCode.None;

        public bool Open(string url)
        {
            Close();
            _h = UnitedAVWebGL.UAV_Web_Create(url);
            return _h != 0;
        }

        // Call once per frame (e.g. from MonoBehaviour.Update) to (re)size the
        // texture once dimensions are known and upload the latest decoded frame.
        public void Tick()
        {
            if (_h == 0) return;
            int w = UnitedAVWebGL.UAV_Web_GetWidth(_h);
            int hh = UnitedAVWebGL.UAV_Web_GetHeight(_h);
            if (w <= 0 || hh <= 0) return;
            if (_tex == null || _w != w || _hgt != hh)
            {
                if (_tex != null) Object.Destroy(_tex);
                _tex = new Texture2D(w, hh, TextureFormat.RGBA32, false, false);
                _tex.wrapMode = TextureWrapMode.Clamp;
                _tex.Apply(false, false);   // realize the GL texture so GetNativeTexturePtr is valid
                _w = w; _hgt = hh;
            }
            if (UnitedAVWebGL.UAV_Web_HasNewFrame(_h) != 0)
            {
                int tex = (int)_tex.GetNativeTexturePtr();
                if (tex != 0)
                {
                    // Dispatch on the active graphics backend: WebGPU player builds use
                    // the GPUTexture copyExternalImageToTexture path; WebGL builds use
                    // texImage2D. (Design-only: the WebGPU jslib branch has a
                    // runtime-verify TODO for the GPUTexture-registry name.)
                    if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.WebGPU)
                        UnitedAVWebGL.UAV_Web_UploadTextureWGPU(_h, tex);
                    else
                        UnitedAVWebGL.UAV_Web_UploadTexture(_h, tex);
                }
            }
        }

        UnitedAVWebGL.State St => _h != 0 ? (UnitedAVWebGL.State)UnitedAVWebGL.UAV_Web_GetState(_h)
                                          : UnitedAVWebGL.State.Idle;

        // IMediaControl
        public bool Play()  { if (_h == 0) return false; UnitedAVWebGL.UAV_Web_Play(_h); return true; }
        public bool Pause() { if (_h == 0) return false; UnitedAVWebGL.UAV_Web_Pause(_h); return true; }
        public bool Stop()  { if (_h == 0) return false; UnitedAVWebGL.UAV_Web_Pause(_h); UnitedAVWebGL.UAV_Web_Seek(_h, 0); return true; }
        public bool Seek(double t) { if (_h == 0) return false; UnitedAVWebGL.UAV_Web_Seek(_h, t); return true; }
        public void SetLooping(bool on) { _looping = on; if (_h != 0) UnitedAVWebGL.UAV_Web_SetLooping(_h, on ? 1 : 0); }
        public bool IsLooping() => _looping;
        public void SetPlaybackRate(float r) { _rate = r; }   // HTMLVideoElement.playbackRate could be wired if needed
        public float GetPlaybackRate() => _rate;
        public bool IsPlaying()   => St == UnitedAVWebGL.State.Playing;
        public bool IsPaused()    => St == UnitedAVWebGL.State.Paused;
        public bool IsFinished()  => St == UnitedAVWebGL.State.Finished;
        public bool IsSeeking()   => St == UnitedAVWebGL.State.Buffering;
        public bool IsBuffering() => St == UnitedAVWebGL.State.Opening || St == UnitedAVWebGL.State.Buffering;
        public double GetCurrentTime() => _h != 0 ? UnitedAVWebGL.UAV_Web_GetPosition(_h) : 0.0;
        public TimeRanges GetBufferedTimes() => new TimeRanges();
        public ErrorCode GetLastError() => St == UnitedAVWebGL.State.Error ? ErrorCode.LoadFailed : _err;

        public void SetVolume(float v) { if (_h != 0) UnitedAVWebGL.UAV_Web_SetVolume(_h, v); }
        public void SetMuted(bool m)   { if (_h != 0) UnitedAVWebGL.UAV_Web_SetMuted(_h, m ? 1 : 0); }

        // IMediaInfo
        public double GetDuration() { double d = _h != 0 ? UnitedAVWebGL.UAV_Web_GetDuration(_h) : 0.0; return d < 0 ? double.PositiveInfinity : d; }
        public int GetVideoWidth()  => _w;
        public int GetVideoHeight() => _hgt;
        public float GetVideoFrameRate() => 0f;   // not exposed by HTMLVideoElement
        public bool HasVideo() => _h != 0 && UnitedAVWebGL.UAV_Web_HasVideo(_h) != 0;
        public bool HasAudio() => false;          // audio plays through the browser, not a PCM tap

        // ITextureProducer
        public Texture GetTexture() => _tex;
        public bool RequiresVerticalFlip() => false;   // jslib uploads with UNPACK_FLIP_Y

        public void Close()
        {
            if (_h != 0) { UnitedAVWebGL.UAV_Web_Destroy(_h); _h = 0; }
            if (_tex != null) { Object.Destroy(_tex); _tex = null; }
            _w = _hgt = 0;
        }
    }
}
#endif
