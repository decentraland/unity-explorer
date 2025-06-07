// SPDX-FileCopyrightText: 2024 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

#if UNITY_2023_3_OR_NEWER || UNITY_2022_3
#define VISION_OS_SUPPORTED
#endif

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KtxUnity.Editor.Tests
{
    class BuildPreProcessorTests
    {
        [Test]
        public void AppleIOSLibraryTypeCheck()
        {
            AppleLibraryTypeCheck(BuildTarget.iOS);
        }

        [Test]
        public void AppleTvOSLibraryTypeCheck()
        {
            AppleLibraryTypeCheck(BuildTarget.tvOS);
        }

        [Test]
        public void AppleVisionOSLibraryTypeCheck()
        {
#if VISION_OS_SUPPORTED
            AppleLibraryTypeCheck(BuildTarget.VisionOS);
#else
            Assert.Ignore("VisionOS is not supported in this Unity version.");
#endif
        }

        static void AppleLibraryTypeCheck(BuildTarget buildTarget)
        {
            var allPlugins = PluginImporter.GetImporters(buildTarget)
                .Where(plugin => plugin.isNativePlugin && plugin.assetPath.StartsWith(BuildPreProcessor.packagePath))
                .ToList();
            Assert.GreaterOrEqual(6, allPlugins.Count);
            foreach (var plugin in allPlugins)
            {
                // Checks that it does not throw an InvalidDataException.
                BuildPreProcessor.IsAppleSimulatorLibrary(plugin.assetPath);
            }
        }
    }
}
