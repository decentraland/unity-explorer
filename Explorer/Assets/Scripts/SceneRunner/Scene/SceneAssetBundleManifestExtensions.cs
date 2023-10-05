﻿using JetBrains.Annotations;
using System;

namespace SceneRunner.Scene
{
    public static class SceneAssetBundleManifestExtensions
    {
        public static string FixCapitalization([CanBeNull] this SceneAssetBundleManifest sceneAssetBundleManifest, string hash)
        {
            if (sceneAssetBundleManifest == null) return hash;

            foreach (string convertedFile in sceneAssetBundleManifest.ConvertedFiles)
            {
                if (string.Compare(hash, convertedFile, StringComparison.OrdinalIgnoreCase) == 0)
                    return convertedFile;
            }

            return hash;
        }
    }
}
