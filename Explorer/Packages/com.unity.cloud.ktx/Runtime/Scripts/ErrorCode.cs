// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;

namespace KtxUnity
{

    /// <summary>
    /// Describes an error during the texture import.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// Texture import was successful.
        /// </summary>
        Success,
        /// <summary>
        /// KTX v1 is not supported.
        /// </summary>
        UnsupportedVersion,
        /// <summary>
        /// The GraphicsFormat is unsupported by KTX Unity.
        /// </summary>
        UnsupportedFormat,
        /// <summary>
        /// The GraphicsFormat is unsupported by the current system.
        /// </summary>
        FormatUnsupportedBySystem,
        /// <summary>
        /// Only super-compressed KTX is supported.
        /// </summary>
        NotSuperCompressed,
        /// <summary>
        /// Loading from URI returned a WebRequest error.
        /// </summary>
        OpenUriFailed,
        /// <summary>
        /// Loading texture data failed.
        /// </summary>
        LoadingFailed,
        /// <summary>
        /// Transcoding to GraphicsFormat failed.
        /// </summary>
        TranscodeFailed,
        /// <summary>
        /// Layer index exceeds layer count.
        /// </summary>
        InvalidLayer,
        /// <summary>
        /// MipMap level exceeds level count.
        /// </summary>
        InvalidLevel,
        /// <summary>
        /// Face slice exceeds face count.
        /// </summary>
        InvalidFace,
        /// <summary>
        /// Face slice exceeds base depth.
        /// </summary>
        InvalidSlice,
    }


    /// <summary>
    /// Used to generate a message from an <see cref="ErrorCode"/>.
    /// </summary>
    public static class ErrorMessage
    {
#if !DEBUG
        const string k_UnknownErrorMessage = "Unknown Error";
#endif
        static readonly Dictionary<ErrorCode, string> k_ErrorMessages = new Dictionary<ErrorCode, string>() {
            { ErrorCode.Success, "OK" },
            { ErrorCode.UnsupportedVersion, "Only KTX 2.0 is supported" },
            { ErrorCode.UnsupportedFormat, "Unsupported format" },
            { ErrorCode.FormatUnsupportedBySystem, "Format not supported by system" },
            { ErrorCode.NotSuperCompressed, "Only super-compressed KTX is supported" },
            { ErrorCode.OpenUriFailed, "Loading URI failed" },
            { ErrorCode.LoadingFailed, "Loading failed" },
            { ErrorCode.TranscodeFailed, "Transcoding failed" },
            { ErrorCode.InvalidLayer, "Invalid ImageIndex" },
            { ErrorCode.InvalidLevel, "Invalid MipMapLevel" },
            { ErrorCode.InvalidFace, "Invalid Face" },
            { ErrorCode.InvalidSlice, "Invalid Slice" },
        };

        /// <summary>
        /// Generates a message from an <see cref="ErrorCode"/>.
        /// </summary>
        /// <param name="code">The <see cref="ErrorCode"/> to generate a message from.</param>
        /// <returns>The generated error message.</returns>
        public static string GetErrorMessage(ErrorCode code)
        {
            if (k_ErrorMessages.TryGetValue(code, out var message))
            {
                return message;
            }
#if DEBUG
            return $"No Error message for error {code.ToString()}";
#else
            return k_UnknownErrorMessage;
#endif
        }
    }
}
