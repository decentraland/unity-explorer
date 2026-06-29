// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace UnitedAV
{
    internal sealed class TextureProducerImpl : ITextureProducer
    {
        private readonly MediaPlayer _player;
        private Texture2D _texture;

        public TextureProducerImpl(MediaPlayer player)
        {
            _player = player;
        }

        public Texture GetTexture()
        {
            return _texture;
        }

        public bool RequiresVerticalFlip()
        {
            // CPU path delivers top-down RGBA rows; Unity samples bottom-up.
            return true;
        }

        internal void SetTexture(Texture2D texture)
        {
            _texture = texture;
        }

        internal void ClearTexture()
        {
            _texture = null;
        }

        internal void Dispose()
        {
            _texture = null;
            _player?.DestroyTextureInternal();
        }
    }
}
