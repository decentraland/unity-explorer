// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace KtxUnity.Editor
{
    abstract class TextureImporter : ScriptedImporter
    {

        /// <summary>
        /// Texture array layer to import.
        /// </summary>
        public uint layer;

        /// <summary>
        /// Cubemap face or 3D/volume texture slice to import.
        /// </summary>
        public uint faceSlice;

        /// <summary>
        /// Lowest mipmap level to import (where 0 is the highest resolution).
        /// Lower mipmap levels (of higher resolution) are being discarded.
        /// Useful to limit texture resolution.
        /// </summary>
        public uint levelLowerLimit;

        /// <summary>
        /// If true, a mipmap chain (if present) is imported.
        /// </summary>
        public bool importLevelChain = true;

        /// <summary>
        /// If true, texture will be sampled
        /// in linear color space (sRGB otherwise)
        /// </summary>
        public bool linear;

        // ReSharper disable once NotAccessedField.Local
        [SerializeField]
        [HideInInspector]
        string[] reportItems;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            Profiler.BeginSample("Import Texture");
            var result = LoadTexture();

            if (result.errorCode == ErrorCode.Success)
            {
                result.texture.name = name;
                result.texture.alphaIsTransparency = true;
                ctx.AddObjectToAsset("result", result.texture);
                ctx.SetMainObject(result.texture);
                reportItems = new string[] { };
            }
            else
            {
                var errorMessage = ErrorMessage.GetErrorMessage(result.errorCode);
                reportItems = new[] { errorMessage };
                Debug.LogError($"Could not load texture file at {assetPath}: {errorMessage}", this);
            }

            Profiler.EndSample();
        }

        protected abstract TextureResult LoadTexture();
    }
}
