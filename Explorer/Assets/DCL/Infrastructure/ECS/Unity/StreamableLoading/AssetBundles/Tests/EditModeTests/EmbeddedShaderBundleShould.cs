using DCL.Utility;
using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class EmbeddedShaderBundleShould
    {
        private const string WINDOWS_SCENE_SHADER_BUNDLE = "dcl/scene_ignore_windows";
        private const string LINUX_SCENE_SHADER_BUNDLE = "dcl/scene_ignore_linux";
        private const string WINDOWS_TEXARRAY_SHADER_BUNDLE = "dcl/scene_texarray_ignore_windows";
        private const string LINUX_TEXARRAY_SHADER_BUNDLE = "dcl/scene_texarray_ignore_linux";
        private const string WINDOWS_CONTENT_BUNDLE = "bafkreicxwj4mhufwziveusb733zs3ly2vz7evnpsbybto3izqtfrbr7umq_windows";

        private const string SCENE_SHADER_NAME = "DCL/Scene";
        private const string SCENE_SHADER_ASSET_NAME = "Scene.shader";

        // The identity content bundles reference: the bundle's internal archive name, stored in the uncompressed
        // directory block at the head of the file.
        private static readonly Regex BUNDLE_IDENTITY = new (@"CAB-[0-9a-f]{32}", RegexOptions.Compiled);
        private const int BUNDLE_HEADER_BYTES = 64 * 1024;

        private static string StreamingAssetsBundle(string relativePath) =>
            Path.Combine(Application.streamingAssetsPath, "AssetBundles", relativePath);

        private static string ReadBundleIdentity(string path)
        {
            using FileStream stream = File.OpenRead(path);
            var header = new byte[BUNDLE_HEADER_BYTES];
            int read = stream.Read(header, 0, header.Length);
            Match match = BUNDLE_IDENTITY.Match(System.Text.Encoding.ASCII.GetString(header, 0, read));
            Assert.That(match.Success, Is.True, $"no bundle identity in the header of {path}");
            return match.Value;
        }

        [Test]
        public void OpenTheLinuxFileForWindowsShaderBundlesOnLinux()
        {
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.LinuxPlayer), Is.EqualTo(LINUX_SCENE_SHADER_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.LinuxEditor), Is.EqualTo(LINUX_SCENE_SHADER_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_TEXARRAY_SHADER_BUNDLE, RuntimePlatform.LinuxPlayer), Is.EqualTo(LINUX_TEXARRAY_SHADER_BUNDLE));
        }

        [Test]
        public void OpenContentBundlesAsNamedOnLinux()
        {
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_CONTENT_BUNDLE, RuntimePlatform.LinuxPlayer), Is.EqualTo(WINDOWS_CONTENT_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName("dcl/scene_ignore_mac", RuntimePlatform.LinuxPlayer), Is.EqualTo("dcl/scene_ignore_mac"));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName("dcl/universal render pipeline/lit_ignore_windows", RuntimePlatform.LinuxPlayer), Is.EqualTo("dcl/universal render pipeline/lit_ignore_windows"));
        }

        [Test]
        public void OpenShaderBundlesAsNamedOnOtherPlatforms()
        {
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.WindowsPlayer), Is.EqualTo(WINDOWS_SCENE_SHADER_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.WindowsEditor), Is.EqualTo(WINDOWS_SCENE_SHADER_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.OSXPlayer), Is.EqualTo(WINDOWS_SCENE_SHADER_BUNDLE));
            Assert.That(PlatformUtils.GetEmbeddedShaderBundleName(WINDOWS_SCENE_SHADER_BUNDLE, RuntimePlatform.WebGLPlayer), Is.EqualTo(WINDOWS_SCENE_SHADER_BUNDLE));
        }

        [TestCase(WINDOWS_SCENE_SHADER_BUNDLE, LINUX_SCENE_SHADER_BUNDLE)]
        [TestCase("lods/" + WINDOWS_TEXARRAY_SHADER_BUNDLE, "lods/" + LINUX_TEXARRAY_SHADER_BUNDLE)]
        public void ShipTheLinuxShaderBundleUnderTheWindowsBundleIdentity(string windowsBundle, string linuxBundle)
        {
            string windowsPath = StreamingAssetsBundle(windowsBundle);
            string linuxPath = StreamingAssetsBundle(linuxBundle);

            Assert.That(File.Exists(windowsPath), Is.True, windowsPath);
            Assert.That(File.Exists(linuxPath), Is.True, linuxPath);
            Assert.That(ReadBundleIdentity(linuxPath), Is.EqualTo(ReadBundleIdentity(windowsPath)));
        }

        [Test]
        public void CarryTheSceneShaderInTheLinuxBundle()
        {
            if (Application.platform != RuntimePlatform.LinuxEditor)
                Assert.Ignore("a StandaloneLinux64 bundle only loads on a Linux editor");

            AssetBundle bundle = AssetBundle.LoadFromFile(StreamingAssetsBundle(LINUX_SCENE_SHADER_BUNDLE));
            Assert.That(bundle, Is.Not.Null);

            try
            {
                Shader shader = bundle.LoadAsset<Shader>(SCENE_SHADER_ASSET_NAME);
                Assert.That(shader, Is.Not.Null);
                Assert.That(shader.name, Is.EqualTo(SCENE_SHADER_NAME));
            }
            finally { bundle.Unload(true); }
        }
    }
}
