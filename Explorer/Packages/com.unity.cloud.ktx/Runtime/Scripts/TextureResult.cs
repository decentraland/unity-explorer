// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using UnityEngine;

namespace KtxUnity
{

    /// <summary>
    /// TextureResult encapsulates result of texture loading. The texture itself and its orientation.
    /// </summary>
    public class TextureResult
    {
        /// <summary>
        /// The successfully imported <see cref="Texture2D"/>.
        /// </summary>
        public Texture2D texture;
        /// <summary>
        /// The <see cref="TextureOrientation"/> of the imported texture.
        /// </summary>
        public TextureOrientation orientation;
        /// <summary>
        /// The <see cref="ErrorCode"/> from the failed texture import.
        /// </summary>
        public ErrorCode errorCode = ErrorCode.Success;

        /// <summary>
        /// Creates an empty <see cref="TextureResult"/>.
        /// </summary>
        public TextureResult() { }

        /// <summary>
        /// Creates an invalid <see cref="TextureResult"/> with an <see cref="ErrorCode"/>.
        /// </summary>
        /// <param name="errorCode">The <see cref="ErrorCode"/> from the failed texture import.</param>
        public TextureResult(ErrorCode errorCode)
        {
            this.errorCode = errorCode;
        }

        /// <summary>
        /// Creates a successful <see cref="TextureResult"/> with a <see cref="Texture2D"/>
        /// and a <see cref="TextureOrientation"/>.
        /// </summary>
        /// <param name="texture">The successfully imported <see cref="Texture2D"/>.</param>
        /// <param name="orientation">The <see cref="TextureOrientation"/> of the imported texture.</param>
        public TextureResult(Texture2D texture, TextureOrientation orientation)
        {
            this.texture = texture;
            this.orientation = orientation;
        }
    }
}
