// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

#if !(UNITY_ANDROID || UNITY_WEBGL) || UNITY_EDITOR
#define LOCAL_LOADING
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking;
using UnityEngine.TestTools;

namespace KtxUnity.Tests
{

    [TestFixture]
    class Loading : IPrebuildSetup
    {
        const string k_URLPrefix = "https://github.com/KhronosGroup/KTX-Software/raw/main/tests/testimages/";

        const string k_StreamingAssetsDir = "ktx-testimages";

        static readonly string[] k_TestTextureAssets =
        {
            "3dtex_7_reference_etc1s.ktx2",
        };

        static readonly string[] k_TestDataUrls = {
            "3dtex_1_reference_u.ktx2",
            "3dtex_7_reference_u.ktx2",
            "alpha_simple_basis.ktx2",
            "arraytex_1_reference_u.ktx2",
            "arraytex_7_mipmap_reference_u.ktx2",
            "arraytex_7_reference_u.ktx2",
            "astc_ldr_4x4_FlightHelmet_baseColor.ktx2",
            "astc_ldr_5x4_Iron_Bars_001_normal.ktx2",
            "astc_ldr_6x5_FlightHelmet_baseColor.ktx2",
            "astc_ldr_6x6_3dtex_7.ktx2",
            "astc_ldr_6x6_arraytex_7.ktx2",
            "astc_ldr_6x6_arraytex_7_mipmap.ktx2",
            "astc_ldr_6x6_Iron_Bars_001_normal.ktx2",
            "astc_ldr_6x6_posx.ktx2",
            "astc_ldr_8x6_FlightHelmet_baseColor.ktx2",
            "astc_ldr_8x8_FlightHelmet_baseColor.ktx2",
            "astc_ldr_10x5_FlightHelmet_baseColor.ktx2",
            "astc_ldr_12x10_FlightHelmet_baseColor.ktx2",
            "astc_ldr_12x12_FlightHelmet_baseColor.ktx2",
            "astc_ldr_cubemap_6x6.ktx2",
            "astc_mipmap_ldr_4x4_posx.ktx2",
            "astc_mipmap_ldr_6x5_posx.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_fast.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_fastest.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_medium.ktx2",
            "astc_mipmap_ldr_6x6_posx.ktx2",
            "astc_mipmap_ldr_6x6_posy.ktx2",
            "astc_mipmap_ldr_6x6_posz.ktx2",
            "astc_mipmap_ldr_8x6_posx.ktx2",
            "astc_mipmap_ldr_8x8_posx.ktx2",
            "astc_mipmap_ldr_10x5_posx.ktx2",
            "astc_mipmap_ldr_12x10_posx.ktx2",
            "astc_mipmap_ldr_12x12_posx.ktx2",
            "astc_mipmap_ldr_cubemap_6x6.ktx2",
            "camera_camera_BaseColor_basis.ktx2",
            "camera_camera_BaseColor_uastc.ktx2",
            "ccwn2c08.ktx2",
            "CesiumLogoFlat.ktx2",
            "cimg5293_uastc.ktx2",
            "cimg5293_uastc_zstd.ktx2",
            "color_grid_basis.ktx2",
            "color_grid_uastc.ktx2",
            "color_grid_uastc_zstd.ktx2",
            "color_grid_zstd.ktx2",
            "cubemap_goldengate_uastc_rdo4_zstd5_rd.ktx2",
            "cubemap_yokohama_basis_rd.ktx2",
            "cyan_rgb_reference_basis.ktx2",
            "cyan_rgb_reference_uastc.ktx2",
            "cyan_rgba_reference_u.ktx2",
            "etc1s_Iron_Bars_001_normal.ktx2",
            "FlightHelmet_baseColor_basis.ktx2",
            "g03n2c08.ktx2",
            "green_rgb_reference_u.ktx2",
            "hűtő.ktx2",
            "hűtő_zstd.ktx2",
            "kodim17_basis.ktx2",
            "ktx_app-u.ktx2",
            "ktx_document_basis.ktx2",
            "ktx_document_uastc_rdo4_zstd5.ktx2",
            "luminance_alpha_reference_basis.ktx2",
            "luminance_alpha_reference_u.ktx2",
            "luminance_alpha_reference_uastc.ktx2",
            "luminance_reference_basis.ktx2",
            "luminance_reference_u.ktx2",
            "luminance_reference_uastc.ktx2",
            "orient-down-metadata-u.ktx2",
            "orient-up-metadata-u.ktx2",
            "orient-up-metadata.ktx2",
            "pattern_02_bc2.ktx2",
            "r_reference_basis.ktx2",
            "r_reference_u.ktx2",
            "r_reference_uastc.ktx2",
            "rg_reference_basis.ktx2",
            "rg_reference_u.ktx2",
            "rg_reference_uastc.ktx2",
            "rgb-mipmap-reference-u.ktx2",
            "rgba-mipmap-reference-basis.ktx2",
            "rgba-reference-u.ktx2",
            "skybox.ktx2",
            "skybox_zstd.ktx2",
            "tbrn2c08.ktx2",
            "tbyn3p08.ktx2",
            "texturearray_astc_8x8_unorm.ktx2",
            "texturearray_bc3_unorm.ktx2",
            "texturearray_etc2_unorm.ktx2",
            "tm3n3p02.ktx2",
            "uastc_Iron_Bars_001_normal.ktx2",
            "نَسِيج.ktx2",
            "نَسِيج_zstd.ktx2",
            "テクスチャ.ktx2",
            "テクスチャ_zstd.ktx2",
            "质地.ktx2",
            "质地_zstd.ktx2",
            "조직.ktx2",
            "조직_zstd.ktx2",
        };

        public void Setup()
        {
#if UNITY_EDITOR
            CreateStreamingAssetsFolder();
            CopyTestAssetsToStreamingAssets();
            DownloadTestData();
            AssetDatabase.Refresh();
#endif
        }

#if UNITY_EDITOR
        static void DownloadTestData()
        {
            var allUrls = new List<string>();
            allUrls.AddRange(k_TestDataUrls);

            foreach (var url in allUrls)
            {
                var destination = GetAbsolutePath(url);
                if (File.Exists(destination))
                {
                    continue;
                }
                var webRequest = UnityWebRequest.Get(k_URLPrefix + url);
                var x = webRequest.SendWebRequest();
                while (!x.isDone)
                {
                    Thread.Sleep(100);
                }
                if (!string.IsNullOrEmpty(webRequest.error))
                {
                    Debug.LogError($"Loading KTX testimages failed!\nError loading {url}: {webRequest.error}");
                    return;
                }

                File.WriteAllBytes(destination, webRequest.downloadHandler.data);
            }
        }

        static void CreateStreamingAssetsFolder()
        {
            var dir = Path.Combine(Application.streamingAssetsPath, k_StreamingAssetsDir);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        static void CopyTestAssetsToStreamingAssets()
        {
            foreach (var asset in k_TestTextureAssets)
            {
                var destination = GetAbsolutePath(asset);
                if (File.Exists(destination))
                {
                    continue;
                }
                FileUtil.CopyFileOrDirectory(
                    $"Packages/com.unity.cloud.ktx/Tests/Textures/{asset}.bytes",
                    destination
                    );
            }
        }
#endif

        static string GetAbsolutePath(string name)
        {
            return Path.Combine(Application.streamingAssetsPath, k_StreamingAssetsDir, name);
        }

        static async Task<NativeArray<byte>> GetTestData(string name)
        {
            var path = GetAbsolutePath(name);

#if LOCAL_LOADING
            path = $"file://{path}";
#endif
            var webRequest = UnityWebRequest.Get(path);
            var x = webRequest.SendWebRequest();
            while (!x.isDone)
            {
                await Task.Yield();
            }
            if (!string.IsNullOrEmpty(webRequest.error))
            {
                Debug.LogErrorFormat("Error loading {0}: {1}", path, webRequest.error);
            }

            return new NativeArray<byte>(webRequest.downloadHandler.data, Allocator.Persistent);
        }

        [KtxTestCase(new[] {
            "3dtex_1_reference_u.ktx2",
            "3dtex_7_reference_u.ktx2",
            "ccwn2c08.ktx2", // R8G8B8_UNorm
            "color_grid_zstd.ktx2",
            "cyan_rgba_reference_u.ktx2",
            "g03n2c08.ktx2", // R8G8B8_SRGB
            "green_rgb_reference_u.ktx2", // R8G8B8_SRGB
            "hűtő.ktx2",
            "hűtő_zstd.ktx2",
            "ktx_app-u.ktx2",
            "luminance_alpha_reference_u.ktx2",
            "luminance_reference_u.ktx2",
            "orient-down-metadata-u.ktx2", // R8G8B8_SRGB
            "orient-up-metadata-u.ktx2", // R8G8B8_SRGB
            "orient-up-metadata.ktx2", // R8G8B8_SRGB
            "r_reference_u.ktx2",
            "rg_reference_u.ktx2",
            "rgb-mipmap-reference-u.ktx2", // R8G8B8_SRGB
            "rgba-reference-u.ktx2",
            "skybox.ktx2", // B10G11R11_UFloatPack32
            "skybox_zstd.ktx2",// B10G11R11_UFloatPack32
            "tbrn2c08.ktx2",
            "tbyn3p08.ktx2",
            "tm3n3p02.ktx2",
            "نَسِيج.ktx2",
            "نَسِيج_zstd.ktx2",
            "テクスチャ.ktx2",
            "テクスチャ_zstd.ktx2",
            "质地.ktx2",
            "质地_zstd.ktx2",
            "조직.ktx2",
            "조직_zstd.ktx2",
        })]
        public IEnumerator Uncompressed(string url)
        {
            yield return RunTest(url, ktxChecks: (code, texture) =>
            {
                Assert.AreEqual(ErrorCode.Success, code);
                Assert.IsFalse(texture.isCompressed);
            });
        }

        [KtxTestCase(new[] {
            "astc_ldr_8x8_FlightHelmet_baseColor.ktx2",
            "astc_mipmap_ldr_8x8_posx.ktx2",
            "pattern_02_bc2.ktx2",
        })]
        public IEnumerator Compressed(string url)
        {
            yield return RunTest(url, ktxChecks: (code, texture) =>
            {
                Assert.AreEqual(ErrorCode.Success, code);
                Assert.IsTrue(texture.isCompressed);
            });
        }

        [KtxTestCase(new[] {
            "arraytex_1_reference_u.ktx2",
            "arraytex_7_mipmap_reference_u.ktx2",
            "arraytex_7_reference_u.ktx2",
            "texturearray_astc_8x8_unorm.ktx2",
            "texturearray_bc3_unorm.ktx2",
            "texturearray_etc2_unorm.ktx2", // RGB_ETC2_UNorm
        })]
        public IEnumerator Array(string url)
        {
            yield return RunTest(url, ktxChecks: (code, texture) =>
            {
                Assert.AreEqual(ErrorCode.Success, code);
                Assert.Greater(texture.numLayers, 0);
            });
        }

        [KtxTestCase(new[] {
            "alpha_simple_basis.ktx2",
            "camera_camera_BaseColor_basis.ktx2",
            "camera_camera_BaseColor_uastc.ktx2",
            "CesiumLogoFlat.ktx2",
            "cimg5293_uastc.ktx2",
            "cimg5293_uastc_zstd.ktx2",
            "color_grid_uastc.ktx2",
            "color_grid_uastc_zstd.ktx2",
            "cubemap_goldengate_uastc_rdo4_zstd5_rd.ktx2",
            "cubemap_yokohama_basis_rd.ktx2",
            "cyan_rgb_reference_basis.ktx2",
            "cyan_rgb_reference_uastc.ktx2",
            "etc1s_Iron_Bars_001_normal.ktx2",
            "FlightHelmet_baseColor_basis.ktx2",
            "kodim17_basis.ktx2",
            "ktx_document_basis.ktx2",
            "ktx_document_uastc_rdo4_zstd5.ktx2",
            "luminance_alpha_reference_basis.ktx2",
            "luminance_alpha_reference_uastc.ktx2",
            "luminance_reference_basis.ktx2",
            "luminance_reference_uastc.ktx2",
            "r_reference_basis.ktx2",
            "r_reference_uastc.ktx2",
            "rg_reference_basis.ktx2",
            "rg_reference_uastc.ktx2",
            "rgba-mipmap-reference-basis.ktx2",
            "uastc_Iron_Bars_001_normal.ktx2",
        })]
        public IEnumerator Basis(string url)
        {
            yield return RunTest(url, ktxChecks: (code, texture) =>
            {
                Assert.AreEqual(ErrorCode.Success, code);
                Assert.IsNotNull(texture);
                Assert.IsTrue(texture.needsTranscoding);
            });
        }


        [KtxTestCase(new[] {
            "rgba-mipmap-reference-basis.ktx2",
        })]
        public IEnumerator Mipmaps(string url)
        {
            yield return RunTest(url, resultCallback: result =>
            {
                Assert.AreEqual(64, result.texture.width);
                Assert.AreEqual(64, result.texture.height);
                Assert.AreEqual(7, result.texture.mipmapCount);
            });

            yield return RunTest(url, mipLevel: 1, mipChain: true, resultCallback: result =>
              {
                  Assert.AreEqual(32, result.texture.width);
                  Assert.AreEqual(32, result.texture.height);
                  Assert.AreEqual(6, result.texture.mipmapCount);
              });

            yield return RunTest(url, mipLevel: 2, mipChain: false, resultCallback: result =>
              {
                  Assert.AreEqual(16, result.texture.width);
                  Assert.AreEqual(16, result.texture.height);
                  Assert.AreEqual(1, result.texture.mipmapCount);
              });
        }

        [KtxTestCase(new[] {
            "alpha_simple_basis.ktx2",
        })]
        public IEnumerator Alpha(string url)
        {
            yield return RunTest(
                url,
                ktxChecks: (code, texture) =>
                {
                    Assert.AreEqual(ErrorCode.Success, code);
                    Assert.IsTrue(texture.hasAlpha);
                });
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator ColorGrid(string url)
        {
            yield return RunTest(
                url,
                ktxChecks: (code, texture) =>
                {
                    Assert.AreEqual(ErrorCode.Success, code);
                    Assert.IsNotNull(texture);
                    Assert.IsTrue(texture.needsTranscoding);
                    Assert.IsFalse(texture.hasAlpha);
                    Assert.IsTrue(texture.isPowerOfTwo);
                    Assert.IsTrue(texture.isMultipleOfFour);
                    Assert.IsTrue(texture.isSquare);
                    Assert.AreEqual(1024, texture.baseWidth);
                    Assert.AreEqual(1024, texture.baseHeight);
                    Assert.AreEqual(1, texture.baseDepth);
                    Assert.AreEqual(TextureOrientation.KtxDefault, texture.orientation);
                });
        }

        [KtxTestCase(new[] {
            "rgba-mipmap-reference-basis.ktx2",
        })]
        public IEnumerator Linear(string url)
        {
            yield return RunTest(url, linear: true, resultCallback: result =>
             {
                 Assert.AreEqual(64, result.texture.width);
                 Assert.AreEqual(64, result.texture.height);
             });
        }

        [KtxTestCase(new[] {
            "3dtex_7_reference_u.ktx2",
        })]
        public IEnumerator Texture3d(string url)
        {
            for (var slice = 0u; slice < 7; slice++)
            {
                yield return RunTest(url, faceSlice: slice, resultCallback: result =>
                 {
                     Assert.AreEqual(16, result.texture.width);
                     Assert.AreEqual(16, result.texture.height);
                 });
            }
        }

        [KtxTestCase(new[] {
            "3dtex_7_reference_etc1s.ktx2",
        })]
        public IEnumerator Texture3dInvalidSlice(string url)
        {
            yield return RunTest(url, faceSlice: 7, resultCallback: result =>
            {
                Assert.AreEqual(ErrorCode.InvalidSlice, result.errorCode);
                Assert.IsNull(result.texture);
            });
        }

        [KtxTestCase(new[] {
            "cubemap_goldengate_uastc_rdo4_zstd5_rd.ktx2",
        })]
        public IEnumerator Cubemap(string url)
        {
            for (var slice = 0u; slice < 6; slice++)
            {
                yield return RunTest(url, faceSlice: slice, mipLevel: 4, mipChain: false, resultCallback: result =>
                   {
                       Assert.AreEqual(64, result.texture.width);
                       Assert.AreEqual(64, result.texture.height);
                       Assert.AreEqual(1, result.texture.mipmapCount);
                   });
            }
        }

        [KtxTestCase(new[] {
            "astc_ldr_10x5_FlightHelmet_baseColor.ktx2",
            "astc_ldr_12x10_FlightHelmet_baseColor.ktx2",
            "astc_ldr_12x12_FlightHelmet_baseColor.ktx2",
            "astc_ldr_4x4_FlightHelmet_baseColor.ktx2",
            "astc_ldr_5x4_Iron_Bars_001_normal.ktx2",
            "astc_ldr_6x5_FlightHelmet_baseColor.ktx2",
            "astc_ldr_6x6_3dtex_7.ktx2",
            "astc_ldr_6x6_Iron_Bars_001_normal.ktx2",
            "astc_ldr_6x6_arraytex_7.ktx2",
            "astc_ldr_6x6_arraytex_7_mipmap.ktx2",
            "astc_ldr_6x6_posx.ktx2",
            "astc_ldr_8x6_FlightHelmet_baseColor.ktx2",
            "astc_ldr_cubemap_6x6.ktx2",
            "astc_mipmap_ldr_10x5_posx.ktx2",
            "astc_mipmap_ldr_12x10_posx.ktx2",
            "astc_mipmap_ldr_12x12_posx.ktx2",
            "astc_mipmap_ldr_4x4_posx.ktx2",
            "astc_mipmap_ldr_6x5_posx.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_fast.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_fastest.ktx2",
            "astc_mipmap_ldr_6x6_kodim17_medium.ktx2",
            "astc_mipmap_ldr_6x6_posx.ktx2",
            "astc_mipmap_ldr_6x6_posy.ktx2",
            "astc_mipmap_ldr_6x6_posz.ktx2",
            "astc_mipmap_ldr_8x6_posx.ktx2",
            "astc_mipmap_ldr_cubemap_6x6.ktx2",
        })]
        public IEnumerator Unsupported(string url)
        {
            yield return RunTest(url, ktxChecks: CertifyFormatNotSupportedCheck);
        }

        [UnityTest]
        public IEnumerator Garbage()
        {
            var garbage = new NativeArray<byte>(new byte[] { 71, 65, 82, 66, 65, 71, 69, 71, 65, 82, 66, 65, 71, 69, 71, 65, 82, 66, 65, 71, 69 }, Allocator.Persistent);
#if DEBUG
            LogAssert.Expect(LogType.Error,"KTX error code UnknownFileFormat");
#endif
            yield return RunTestAsync(garbage, ktxChecks: (code, texture) =>
            {
                Assert.AreEqual(ErrorCode.LoadingFailed, code);
                Assert.Pass();
            });
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromStreamingAssets(string name)
        {
            var texture = new KtxTexture();
            var path = Path.Combine(k_StreamingAssetsDir, name);
            var task = texture.LoadFromStreamingAssets(path);
            yield return WaitForTask(task);
            var result = task.Result;
            Assert.AreEqual(ErrorCode.Success, result.errorCode);
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromStreamingAssetsFormat(string name)
        {
            var texture = new KtxTexture();
            var path = Path.Combine(k_StreamingAssetsDir, name);
            var task = texture.LoadFromStreamingAssets(path, GraphicsFormat.R8G8B8A8_SRGB);
            yield return WaitForTask(task);
            var result = task.Result;
            Assert.AreEqual(ErrorCode.Success, result.errorCode);
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromUrl(string name)
        {
            var texture = new KtxTexture();
            var url = $"{k_URLPrefix}{name}";
            var task = texture.LoadFromUrl(url);
            yield return WaitForTask(task);
            var result = task.Result;
            Assert.AreEqual(ErrorCode.Success, result.errorCode);
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromUrlFormat(string name)
        {
            var texture = new KtxTexture();
            var url = $"{k_URLPrefix}{name}";
            var task = texture.LoadFromUrl(url, GraphicsFormat.R8G8B8A8_SRGB);
            yield return WaitForTask(task);
            var result = task.Result;
            Assert.AreEqual(ErrorCode.Success, result.errorCode);
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromBytes(string name)
        {
            yield return WaitForTask(LoadFromBytesAsync(name));
        }

        [KtxTestCase(new[] {
            "color_grid_basis.ktx2",
        })]
        public IEnumerator LoadFromBytesFormat(string name)
        {
            yield return WaitForTask(LoadFromBytesAsync(name, GraphicsFormat.R8G8B8A8_SRGB));
        }

        static async Task LoadFromBytesAsync(string name, GraphicsFormat? format = null)
        {
            var texture = new KtxTexture();
            var data = await GetTestData(name);
            try
            {
                var result = format.HasValue
                    ? await texture.LoadFromBytes(data, format.Value)
                    : await texture.LoadFromBytes(data);
                Assert.AreEqual(ErrorCode.Success, result.errorCode);
            }
            finally
            {
                data.Dispose();
            }
        }

        static IEnumerator RunTest(
            string url,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true,
            Action<TextureResult> resultCallback = null,
            Action<ErrorCode, KtxTexture> ktxChecks = null
        )
        {
            var task = RunTestAsync(url, linear, layer, faceSlice, mipLevel, mipChain, resultCallback, ktxChecks);
            yield return WaitForTask(task);
        }

        static async Task RunTestAsync(
            string url,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true,
            Action<TextureResult> resultCallback = null,
            Action<ErrorCode, KtxTexture> ktxChecks = null
        )
        {
            var data = await GetTestData(url);
            try
            {
                await RunTestAsync(data, linear, layer, faceSlice, mipLevel, mipChain, resultCallback, ktxChecks);
            }
            finally
            {
                data.Dispose();
            }
        }

        static async Task RunTestAsync(
            NativeArray<byte> data,
            bool linear = false,
            uint layer = 0,
            uint faceSlice = 0,
            uint mipLevel = 0,
            bool mipChain = true,
            Action<TextureResult> resultCallback = null,
            Action<ErrorCode, KtxTexture> ktxChecks = null
        )
        {
            var texture = new KtxTexture();

            KtxNativeInstance.CertifySupportedPlatform();
            var errorCode = texture.Open(data);
            try
            {
                if (ktxChecks == null)
                {
                    Assert.AreEqual(ErrorCode.Success, errorCode);
                }
                else
                {
                    ktxChecks.Invoke(errorCode, texture);
                }
            }
            catch (IgnoreException)
            {
                texture.Dispose();
                throw;
            }

            var result = await texture.LoadTexture2D(linear, layer, faceSlice, mipLevel, mipChain);
            texture.Dispose();

            if (result.errorCode == ErrorCode.FormatUnsupportedBySystem)
            {
                Assert.Ignore($"GraphicsFormat is not supported on this device.");
            }

            if (resultCallback != null)
            {
                resultCallback(result);
            }
            else
            {
                DefaultResultCheck(result);
            }
        }

        static void CertifyFormatNotSupportedCheck(ErrorCode code, KtxTexture texture)
        {
            Assert.AreEqual(ErrorCode.Success, code);
#if DEBUG
            LogAssert.Expect(LogType.Error,"You're trying to load an untested/unsupported format. Please enter the correct format conversion in `KtxNativeInstance.cs`, test it and make a pull request. Otherwise please open an issue with a sample file.");
#endif
            Assert.AreEqual(true, texture.isCompressed);
            var graphicsFormat = texture.GetGraphicsFormat();
            Assert.AreEqual(GraphicsFormat.None, graphicsFormat);
            Assert.IsFalse(TranscodeFormatHelper.IsFormatSupported(graphicsFormat));
            Assert.Ignore($"GraphicsFormat is not supported.");
        }

        static void DefaultResultCheck(TextureResult result)
        {
            Assert.AreEqual(ErrorCode.Success, result.errorCode);
            Assert.NotNull(result.texture);
        }

        /// <summary>
        /// Wraps a <see cref="Task"/> in an <see cref="IEnumerator"/>.
        /// </summary>
        /// <param name="task">The async Task to wait form</param>
        /// <param name="timeout">Optional timeout in seconds</param>
        /// <returns>IEnumerator</returns>
        /// <exception cref="AggregateException"></exception>
        /// <exception cref="TimeoutException">Thrown when a timout was set and the task took too long</exception>
        static IEnumerator WaitForTask(Task task, float timeout = -1)
        {
            var startTime = Time.realtimeSinceStartup;

            while (!task.IsCompleted)
            {
                CheckExceptionAndTimeout();
                yield return null;
            }

            CheckExceptionAndTimeout();
            yield break;

            void CheckExceptionAndTimeout()
            {
                if (task.Exception != null)
                {
                    if (task.Exception.InnerException != null)
                    {
                        throw task.Exception.InnerException;
                    }
                    throw task.Exception;
                }
                if (timeout > 0 && Time.realtimeSinceStartup - startTime > timeout)
                {
                    throw new TimeoutException();
                }
            }
        }
    }
}
