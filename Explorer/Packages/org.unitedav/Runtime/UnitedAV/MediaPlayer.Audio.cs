// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnitedAV.Internal;

namespace UnitedAV
{
    public partial class MediaPlayer
    {
        private AudioClip _audioClip;
        private int _audioChannels;
        private int _audioSampleRate;

        private float[] _audioScratch;
        private GCHandle _audioScratchHandle;
        private bool _audioScratchPinned;

        // Snapshot for the audio thread; the main thread may zero _native during teardown.
        private IntPtr _audioNative = IntPtr.Zero;

        private const int StreamClipLengthSamples = 16384;

        private void SetupAudioIfNeeded()
        {
            if (_native == IntPtr.Zero)
                return;
            if (_audioClip != null)
                return;

            UAVMediaInfo info;
            if (UnitedAVNative.uav_get_info(_native, out info) != (int)UAVResult.Ok)
                return;
            if (info.has_audio == 0 || info.audio_channels <= 0)
                return;

            _audioChannels = info.audio_channels;
            _audioSampleRate = AudioSettings.outputSampleRate > 0
                ? AudioSettings.outputSampleRate
                : (info.audio_sample_rate > 0 ? info.audio_sample_rate : 48000);

            _audioScratch = new float[8192 * _audioChannels];
            _audioScratchHandle = GCHandle.Alloc(_audioScratch, GCHandleType.Pinned);
            _audioScratchPinned = true;

            _audioNative = _native;

            _audioClip = AudioClip.Create(
                name: "UnitedAV_AudioClip",
                lengthSamples: StreamClipLengthSamples,
                channels: _audioChannels,
                frequency: _audioSampleRate,
                stream: true,
                pcmreadercallback: PcmReader);

            if (_audioSource == null)
            {
                if (!TryGetComponent(out _audioSource))
                    _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.clip = _audioClip;
            _audioSource.loop = true;
            _audioSource.volume = _audioVolume;
            _audioSource.playOnAwake = false;
        }

        // Audio-thread callback. Always fills the whole span (zero-pad on underrun)
        // so the streaming clip never reports "finished".
        private void PcmReader(float[] data)
        {
            IntPtr native = _audioNative;
            if (native == IntPtr.Zero || !_audioScratchPinned || _audioChannels <= 0)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }

            int channels = _audioChannels;
            int framesRequested = data.Length / channels;
            int framesFilled = 0;

            IntPtr scratchPtr = _audioScratchHandle.AddrOfPinnedObject();
            int scratchFrames = _audioScratch.Length / channels;

            while (framesFilled < framesRequested)
            {
                int chunkFrames = Math.Min(scratchFrames, framesRequested - framesFilled);

                int got = UnitedAVNative.uav_read_audio(
                    native, scratchPtr, chunkFrames, channels, _audioSampleRate);

                if (got <= 0)
                    break;

                Array.Copy(_audioScratch, 0, data, framesFilled * channels, got * channels);
                framesFilled += got;

                if (got < chunkFrames)
                    break;
            }

            int filledSamples = framesFilled * channels;
            if (filledSamples < data.Length)
                Array.Clear(data, filledSamples, data.Length - filledSamples);
        }

        private void EnsureAudioPlaying()
        {
            if (_audioSource != null && _audioClip != null && !_audioSource.isPlaying)
                _audioSource.Play();
        }

        private void EnsureAudioPaused()
        {
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Pause();
        }

        private void TeardownAudio()
        {
            // Stop the audio thread from touching native before destroying anything.
            _audioNative = IntPtr.Zero;

            if (_audioSource != null)
            {
                if (_audioSource.isPlaying)
                    _audioSource.Stop();
                if (_audioSource.clip == _audioClip)
                    _audioSource.clip = null;
            }

            if (_audioClip != null)
            {
                Destroy(_audioClip);
                _audioClip = null;
            }

            if (_audioScratchPinned)
            {
                _audioScratchHandle.Free();
                _audioScratchPinned = false;
            }
            _audioScratch = null;
            _audioChannels = 0;
            _audioSampleRate = 0;
        }
    }
}
