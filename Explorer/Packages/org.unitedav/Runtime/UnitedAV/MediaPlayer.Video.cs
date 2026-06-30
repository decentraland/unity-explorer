// SPDX-License-Identifier: Apache-2.0
// CPU video upload path. Textures are RGBA32 sRGB and require a vertical flip.

using System;
using UnityEngine;
using UnitedAV.Internal;

namespace UnitedAV
{
    public partial class MediaPlayer
    {
        private Texture2D _texture;
        private int _texWidth;
        private int _texHeight;

        private long _lastFrameId = -1;

        private void UpdateVideoTexture()
        {
            if (!_mediaOpened || _native == IntPtr.Zero)
                return;

            UAVVideoFrame frame;
            int rc = UnitedAVNative.uav_acquire_frame(_native, _lastFrameId, out frame);

            if (rc != (int)UAVResult.Ok)
                return;

            try
            {
                if (frame.data == IntPtr.Zero || frame.width <= 0 || frame.height <= 0)
                    return;

                if (frame.format != (int)UAVPixelFormat.Rgba32)
                    return;

                EnsureTexture(frame.width, frame.height);
                if (_texture == null)
                    return;

                // ABI contract: RGBA32 is tightly packed (stride == width*4).
                int expected = frame.width * frame.height * 4;
                _texture.LoadRawTextureData(frame.data, expected);
                _texture.Apply(false);

                _lastFrameId = frame.frame_id;

                if (!_firstFrameFired)
                {
                    _firstFrameFired = true;
                    FireEvent(MediaPlayerEvent.EventType.FirstFrameReady, ErrorCode.None);
                }
            }
            finally
            {
                // Native frame buffer is valid only until release.
                UnitedAVNative.uav_release_frame(_native);
            }
        }

        private void EnsureTexture(int width, int height)
        {
            if (_texture != null && _texWidth == width && _texHeight == height)
                return;

            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }

            // linear:false => sRGB texture, matching the sRGB-encoded decoded color.
            _texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "UnitedAV_VideoTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            _texWidth = width;
            _texHeight = height;

            _textureProducer?.SetTexture(_texture);

            FireEvent(MediaPlayerEvent.EventType.ResolutionChanged, ErrorCode.None);
        }

        internal Texture2D CurrentTexture => _texture;

        internal void DestroyTextureInternal()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
            _texWidth = 0;
            _texHeight = 0;
            _lastFrameId = -1;
        }
    }
}
