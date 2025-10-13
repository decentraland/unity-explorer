// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace KtxUnity.Editor
{
    [ScriptedImporter(0, new[] { ".basis" })]
    class BasisImporter : TextureImporter
    {
        protected override TextureResult LoadTexture()
        {
            Profiler.BeginSample("LoadTexture");
            var texture = new BasisUniversalTexture();
            var result = AsyncHelpers.RunSync(() =>
            {
                using (var alloc = new ManagedNativeArray(File.ReadAllBytes(assetPath)))
                {
                    return texture.LoadFromBytesInternal(
                        alloc.nativeArray,
                        linear,
                        layer,
                        faceSlice,
                        levelLowerLimit,
                        importLevelChain
                    );
                }
            });
            Profiler.EndSample();
            return result;
        }
    }
}
