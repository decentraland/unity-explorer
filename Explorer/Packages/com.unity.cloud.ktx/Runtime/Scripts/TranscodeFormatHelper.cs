// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if KTX_VERBOSE
using System.Text;
using Enum = System.Enum;
#endif

namespace KtxUnity
{

    /// <summary>
    /// Mask of texture features
    /// </summary>
    [Flags]
    enum TextureFeatures
    {
        None = 0x0,

        /// <summary>
        /// Format with 4 channels (RGB+Alpha)
        /// </summary>
        AlphaChannel = 0x1,

        /// <summary>
        /// Format supports arbitrary resolutions
        /// </summary>
        NonPowerOfTwo = 0x2,

        /// <summary>
        /// Format supports arbitrary resolutions
        /// </summary>
        NonMultipleOfFour = 0x4,

        /// <summary>
        /// Non square resolution
        /// </summary>
        NonSquare = 0x8,

        /// <summary>
        /// Linear value encoding (not sRGB)
        /// </summary>
        Linear = 0x10
    }

    struct TranscodeFormatTuple
    {
        public GraphicsFormat format;
        public TranscodeFormat transcodeFormat;

        public TranscodeFormatTuple(GraphicsFormat format, TranscodeFormat transcodeFormat)
        {
            this.format = format;
            this.transcodeFormat = transcodeFormat;
        }
    }

    struct FormatInfo
    {
        public TextureFeatures features;
        public TranscodeFormatTuple formats;

        public FormatInfo(TextureFeatures features, GraphicsFormat format, TranscodeFormat transcodeFormat)
        {
            this.features = features;
            this.formats = new TranscodeFormatTuple(format, transcodeFormat);
        }
    }

    static class TranscodeFormatHelper
    {
        static bool s_Initialized;
        static Dictionary<TextureFeatures, TranscodeFormatTuple> s_FormatCache;
        static List<FormatInfo> s_AllFormats;

        static void InitInternal()
        {
            s_Initialized = true;
            s_FormatCache = new Dictionary<TextureFeatures, TranscodeFormatTuple>();

            if (s_AllFormats == null)
            {

                s_AllFormats = new List<FormatInfo>();

                // Add all formats into the List ordered. First supported match will be used.
                // This particular order is based on memory size (1st degree)
                // and a combination of quality and transcode speed (2nd degree)
                // source <http://richg42.blogspot.com/2018/05/basis-universal-gpu-texture-format.html>

                // Compressed
                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare,
                    GraphicsFormat.RGB_ETC2_SRGB,
                    TranscodeFormat.ETC1_RGB));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RGB_ETC_UNorm,
                    TranscodeFormat.ETC1_RGB));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare,
#if UNITY_2018_3_OR_NEWER
                    GraphicsFormat.RGBA_DXT1_SRGB,
#else
                    GraphicsFormat.RGB_DXT1_SRGB,
#endif
                    TranscodeFormat.BC1_RGB));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
#if UNITY_2018_3_OR_NEWER
                    GraphicsFormat.RGBA_DXT1_UNorm,
#else
                    GraphicsFormat.RGB_DXT1_UNorm,
#endif
                    TranscodeFormat.BC1_RGB));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonMultipleOfFour,
                    GraphicsFormat.RGB_PVRTC_4Bpp_SRGB,
                    TranscodeFormat.PVRTC1_4_RGB));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonMultipleOfFour,
                    GraphicsFormat.RGB_PVRTC_4Bpp_UNorm,
                    TranscodeFormat.PVRTC1_4_RGB));

                // Compressed with alpha channel
                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare,
                    GraphicsFormat.RGBA_ASTC4X4_SRGB,
                    TranscodeFormat.ASTC_4x4_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RGBA_ASTC4X4_UNorm,
                    TranscodeFormat.ASTC_4x4_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare,
                    GraphicsFormat.RGBA_ETC2_SRGB,
                    TranscodeFormat.ETC2_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RGBA_ETC2_UNorm,
                    TranscodeFormat.ETC2_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare,
                    GraphicsFormat.RGBA_BC7_SRGB,
                    TranscodeFormat.BC7_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RGBA_BC7_UNorm,
                    TranscodeFormat.BC7_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare,
                    GraphicsFormat.RGBA_DXT5_SRGB,
                    TranscodeFormat.BC3_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RGBA_DXT5_UNorm,
                    TranscodeFormat.BC3_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonMultipleOfFour,
                    GraphicsFormat.RGBA_PVRTC_4Bpp_SRGB,
                    TranscodeFormat.PVRTC1_4_RGBA));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.Linear | TextureFeatures.NonMultipleOfFour,
                    GraphicsFormat.RGBA_PVRTC_4Bpp_UNorm,
                    TranscodeFormat.PVRTC1_4_RGBA));

                // Uncompressed
                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.R5G6B5_UNormPack16,
                    TranscodeFormat.RGB565));

                // Uncompressed with alpha channel
                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare,
                    GraphicsFormat.R8G8B8A8_SRGB, // Also supports SNorm, UInt, SInt
                    TranscodeFormat.RGBA32));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.R8G8B8A8_UNorm, // Also supports SNorm, UInt, SInt
                    TranscodeFormat.RGBA32));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.R4G4B4A4_UNormPack16,
                    TranscodeFormat.RGBA4444));

                // Need to extend TextureFeatures to request single/dual channel texture formats.
                // Until then, those formats are at the end of the list
                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.NonPowerOfTwo | TextureFeatures.NonMultipleOfFour | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.B5G6R5_UNormPack16,
                    TranscodeFormat.BGR565));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.R_EAC_UNorm, // Also supports SNorm
                    TranscodeFormat.ETC2_EAC_R11));

                s_AllFormats.Add(new FormatInfo(
                    TextureFeatures.AlphaChannel | TextureFeatures.NonPowerOfTwo | TextureFeatures.NonSquare | TextureFeatures.Linear,
                    GraphicsFormat.RG_EAC_UNorm, // Also supports SNorm
                    TranscodeFormat.ETC2_EAC_RG11));

                // GraphicsFormat.RGB_A1_ETC2_SRGB,TranscodeFormat.ETC2_RGBA // Does not work; always transcodes 8-bit alpha
                // GraphicsFormat.RGBA_ETC2_SRGB,TranscodeFormat.ATC_RGBA // Not sure if this works (maybe compatible) - untested
                // GraphicsFormat.RGB_ETC_UNorm,ATC_RGB // Not sure if this works (maybe compatible) - untested

                // Not supported via KTX atm
                // GraphicsFormat.R_BC4_UNorm,TranscodeFormat.BC4_R
                // GraphicsFormat.RG_BC5_UNorm,TranscodeFormat.BC5_RG

                // Supported by BasisU, but no matching Unity GraphicsFormat
                // GraphicsFormat.?,TranscodeFormat.ATC_RGB
                // GraphicsFormat.?,TranscodeFormat.ATC_RGBA
                // GraphicsFormat.?,TranscodeFormat.FXT1_RGB
                // GraphicsFormat.?,TranscodeFormat.PVRTC2_4_RGB
                // GraphicsFormat.?,TranscodeFormat.PVRTC2_4_RGBA
            }
        }

        public static void Init()
        {
            if (!s_Initialized)
            {
                InitInternal();
#if KTX_VERBOSE
                CheckTextureSupport();
#endif
            }
        }

        internal static TranscodeFormatTuple? GetFormatsForImage(
            IMetaData meta,
            ILevelInfo li,
            bool linear = false
            )
        {
            var formats = TranscodeFormatHelper.GetPreferredFormat(
                meta.hasAlpha,
                li.isPowerOfTwo,
                li.isMultipleOfFour,
                li.isSquare,
                linear
            );

            if (!formats.HasValue && meta.hasAlpha)
            {
                // Fall back to transcode RGB-only to RGBA texture
                formats = TranscodeFormatHelper.GetPreferredFormat(
                    false,
                    li.isPowerOfTwo,
                    li.isMultipleOfFour,
                    li.isSquare,
                    linear
                    );
            }
            return formats;
        }

        public static TranscodeFormatTuple? GetPreferredFormat(
            bool hasAlpha,
            bool isPowerOfTwo,
            bool isMultipleOfFour,
            bool isSquare,
            bool isLinear = false
        )
        {
            TextureFeatures features = TextureFeatures.None;
            if (hasAlpha)
            {
                features |= TextureFeatures.AlphaChannel;
            }
            if (!isPowerOfTwo)
            {
                features |= TextureFeatures.NonPowerOfTwo;
            }
            if (!isMultipleOfFour)
            {
                features |= TextureFeatures.NonMultipleOfFour;
            }
            if (!isSquare)
            {
                features |= TextureFeatures.NonSquare;
            }
            if (isLinear)
            {
                features |= TextureFeatures.Linear;
            }

            if (s_FormatCache.TryGetValue(features, out var formatTuple))
            {
                return formatTuple;
            }
            else
            {
                foreach (var formatInfo in s_AllFormats)
                {
                    if (!FormatIsMatch(features, formatInfo.features))
                    {
                        continue;
                    }
                    var supported = IsFormatSupported(formatInfo.formats.format, isLinear);
                    if (supported)
                    {
                        s_FormatCache[features] = formatInfo.formats;
                        return formatInfo.formats;
                    }
                }
#if DEBUG
                Debug.LogErrorFormat("Could not find transcode texture format! (alpha:{0} Po2:{1} sqr:{2})",hasAlpha,isPowerOfTwo,isSquare);
#endif
                return null;
            }
        }

        /// <summary>
        /// Takes a desired target format and returns a fitting <see cref="TranscodeFormatTuple"/>, if one was found.
        /// </summary>
        /// <param name="graphicsFormat">Desired target texture format</param>
        /// <returns>A fitting <see cref="TranscodeFormatTuple"/>, if one was found. False otherwise.</returns>
        internal static TranscodeFormatTuple? GetTranscodeFormats(GraphicsFormat graphicsFormat)
        {
            TranscodeFormat? tf;
            switch (graphicsFormat)
            {
                case GraphicsFormat.RGB_ETC_UNorm:
                case GraphicsFormat.RGB_ETC2_SRGB:
                case GraphicsFormat.RGB_ETC2_UNorm:
                    tf = TranscodeFormat.ETC1_RGB;
                    break;
                case GraphicsFormat.RGBA_DXT1_SRGB:
                case GraphicsFormat.RGBA_DXT1_UNorm:
                    tf = TranscodeFormat.BC1_RGB;
                    break;
                case GraphicsFormat.RGBA_DXT5_SRGB:
                case GraphicsFormat.RGBA_DXT5_UNorm:
                    tf = TranscodeFormat.BC3_RGBA;
                    break;
                case GraphicsFormat.RG_BC5_UNorm:
                case GraphicsFormat.RG_BC5_SNorm:
                    tf = TranscodeFormat.BC5_RG;
                    break;
                case GraphicsFormat.R_BC4_UNorm:
                case GraphicsFormat.R_BC4_SNorm:
                    tf = TranscodeFormat.BC4_R;
                    break;
                case GraphicsFormat.RGBA_BC7_SRGB:
                case GraphicsFormat.RGBA_BC7_UNorm:
                    tf = TranscodeFormat.BC7_RGBA;
                    break;

                // // RGB formats are untested.
                // case GraphicsFormat.B8G8R8_SInt:
                // case GraphicsFormat.B8G8R8_SNorm:
                // case GraphicsFormat.B8G8R8_SRGB:
                // case GraphicsFormat.B8G8R8_UInt:
                // case GraphicsFormat.B8G8R8_UNorm:
                // case GraphicsFormat.R8G8B8_SInt:
                // case GraphicsFormat.R8G8B8_SNorm:
                // case GraphicsFormat.R8G8B8_SRGB:
                // case GraphicsFormat.R8G8B8_UInt:
                // case GraphicsFormat.R8G8B8_UNorm:
                //     tf = TranscodeFormat.RGBA32;
                //     break;

                case GraphicsFormat.B8G8R8A8_SInt:
                case GraphicsFormat.B8G8R8A8_SNorm:
                case GraphicsFormat.B8G8R8A8_SRGB:
                case GraphicsFormat.B8G8R8A8_UInt:
                case GraphicsFormat.B8G8R8A8_UNorm:
                case GraphicsFormat.R8G8B8A8_SInt:
                case GraphicsFormat.R8G8B8A8_SNorm:
                case GraphicsFormat.R8G8B8A8_SRGB:
                case GraphicsFormat.R8G8B8A8_UInt:
                case GraphicsFormat.R8G8B8A8_UNorm:
                    tf = TranscodeFormat.RGBA32;
                    break;
                case GraphicsFormat.B5G6R5_UNormPack16:
                case GraphicsFormat.R5G6B5_UNormPack16:
                    tf = TranscodeFormat.BGR565;
                    break;
                case GraphicsFormat.B4G4R4A4_UNormPack16:
                case GraphicsFormat.R4G4B4A4_UNormPack16:
                    tf = TranscodeFormat.RGBA4444;
                    break;
                case GraphicsFormat.RGBA_ASTC4X4_SRGB:
#if UNITY_2020_2_OR_NEWER
                case GraphicsFormat.RGBA_ASTC4X4_UFloat:
#endif
                case GraphicsFormat.RGBA_ASTC4X4_UNorm:
                    tf = TranscodeFormat.ASTC_4x4_RGBA;
                    break;
                case GraphicsFormat.RGBA_ETC2_SRGB:
                case GraphicsFormat.RGBA_ETC2_UNorm:
                    tf = TranscodeFormat.ETC2_RGBA;
                    break;
                case GraphicsFormat.RGBA_PVRTC_4Bpp_SRGB:
                case GraphicsFormat.RGBA_PVRTC_4Bpp_UNorm:
                    tf = TranscodeFormat.PVRTC1_4_RGBA;
                    break;
                case GraphicsFormat.RGB_PVRTC_4Bpp_SRGB:
                case GraphicsFormat.RGB_PVRTC_4Bpp_UNorm:
                    tf = TranscodeFormat.PVRTC1_4_RGB;
                    break;
                case GraphicsFormat.R_EAC_SNorm:
                case GraphicsFormat.R_EAC_UNorm:
                    tf = TranscodeFormat.ETC2_EAC_R11;
                    break;
                case GraphicsFormat.RG_EAC_SNorm:
                case GraphicsFormat.RG_EAC_UNorm:
                    tf = TranscodeFormat.ETC2_EAC_RG11;
                    break;
                default:
                    return null;
            }

            return new TranscodeFormatTuple(graphicsFormat, tf.Value);
        }

        static bool FormatIsMatch(TextureFeatures required, TextureFeatures provided)
        {
            return (required & provided) == required;
        }

        internal static bool IsFormatSupported(GraphicsFormat graphicsFormat, bool linear = false)
        {
            return SystemInfo.IsFormatSupported(
                graphicsFormat,
#if UNITY_2023_2_OR_NEWER
                linear ? GraphicsFormatUsage.Linear : GraphicsFormatUsage.Sample
#else
                linear ? FormatUsage.Linear : FormatUsage.Sample
#endif
            );
        }

#if KTX_VERBOSE
        // ReSharper disable Unity.PerformanceAnalysis
        static void CheckTextureSupport () {
            // Dummy call to force logging all supported formats to console
            GetSupportedTextureFormats(out _);
        }

        static void GetSupportedTextureFormats (
            out List<TranscodeFormatTuple> graphicsFormats
        )
        {
            graphicsFormats = new List<TranscodeFormatTuple>();

            var sb = new StringBuilder();
            foreach(var formatInfo in s_AllFormats) {
                var supported = IsFormatSupported(formatInfo.formats.format);
                if(supported) {
                    graphicsFormats.Add(formatInfo.formats);
                }
                sb.AppendFormat("{0} support: {1}\n",formatInfo.formats.format,supported);
            }

            Debug.Log(sb.ToString());

            sb.Clear();

            var allGfxFormats = (GraphicsFormat[]) Enum.GetValues(typeof(GraphicsFormat));
            foreach(var format in allGfxFormats) {
                sb.Append(format).Append(' ');
                var usages = new[]
                {
#if UNITY_2023_2_OR_NEWER
                    GraphicsFormatUsage.Sample,
                    GraphicsFormatUsage.Blend,
                    GraphicsFormatUsage.GetPixels,
                    GraphicsFormatUsage.Linear,
                    GraphicsFormatUsage.LoadStore,
                    GraphicsFormatUsage.MSAA2x,
                    GraphicsFormatUsage.MSAA4x,
                    GraphicsFormatUsage.MSAA8x,
                    GraphicsFormatUsage.ReadPixels,
                    GraphicsFormatUsage.Render,
                    GraphicsFormatUsage.SetPixels,
                    GraphicsFormatUsage.SetPixels32,
                    GraphicsFormatUsage.Sparse,
                    GraphicsFormatUsage.StencilSampling,
#else
                    FormatUsage.Sample,
                    FormatUsage.Blend,
                    FormatUsage.GetPixels,
                    FormatUsage.Linear,
                    FormatUsage.LoadStore,
                    FormatUsage.MSAA2x,
                    FormatUsage.MSAA4x,
                    FormatUsage.MSAA8x,
                    FormatUsage.ReadPixels,
                    FormatUsage.Render,
                    FormatUsage.SetPixels,
                    FormatUsage.SetPixels32,
                    FormatUsage.Sparse,
                    FormatUsage.StencilSampling,
#endif
                };
                foreach (var usage in usages)
                {
                    sb
                        .Append(usage)
                        .Append(':')
                        .Append(SystemInfo.IsFormatSupported(format, usage) ? "1" : "0")
                        .Append(' ');
                }

                sb.Append('\n');
            }

            Debug.Log(sb.ToString());
        }
#endif
    }
}
