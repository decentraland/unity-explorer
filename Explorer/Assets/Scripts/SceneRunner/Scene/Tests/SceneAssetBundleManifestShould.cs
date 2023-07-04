﻿using NUnit.Framework;
using UnityEngine;

namespace SceneRunner.Scene.Tests
{
    public class SceneAssetBundleManifestShould
    {
        [Test]
        public void ComputeVersionedHash()
        {
            unsafe
            {
                const string CONTENT_URL = "https://content-assets-as-bundle.decentraland.org/v125/";
                const string HASH = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                const string EXPECTED = "v125QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                var manifest = new SceneAssetBundleManifest(CONTENT_URL, new SceneAbDto());

                fixed (char* p = EXPECTED) { Assert.AreEqual(Hash128.Compute(p, (ulong)(EXPECTED.Length * sizeof(char))), manifest.ComputeHash(HASH)); }
            }
        }

        [Test]
        public void ComputeUnversionedHash()
        {
            unsafe
            {
                const string CONTENT_URL = "https://content-assets-as-bundle.decentraland.org/";
                const string HASH = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                const string EXPECTED = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                var manifest = new SceneAssetBundleManifest(CONTENT_URL, new SceneAbDto());

                fixed (char* p = EXPECTED) { Assert.AreEqual(Hash128.Compute(p, (ulong)(EXPECTED.Length * sizeof(char))), manifest.ComputeHash(HASH)); }
            }
        }
    }
}
