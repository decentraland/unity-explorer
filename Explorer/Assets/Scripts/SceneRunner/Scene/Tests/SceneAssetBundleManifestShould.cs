using System;
using CommunicationData.URLHelpers;
using NUnit.Framework;
using UnityEngine;

namespace SceneRunner.Scene.Tests
{
    public class SceneAssetBundleManifestShould
    {
        [Test]
        public void ComputeDatedHash()
        {
            unsafe
            {
                const string CONTENT_URL = "https://content-assets-as-bundle.decentraland.org/";
                const string HASH = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";
                const string DATE = "06_10_2024";
                const string EXPECTED = "06_10_2024QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                var manifest = new SceneAssetBundleManifest(URLDomain.FromString(CONTENT_URL), "v125", Array.Empty<string>(), HASH,DATE);
                fixed (char* p = EXPECTED) { Assert.AreEqual(Hash128.Compute(p, (ulong)(EXPECTED.Length * sizeof(char))), manifest.ComputeHash(HASH)); }
            }
        }

        [Test]
        public void ComputeUndatedHash()
        {
            unsafe
            {
                const string CONTENT_URL = "https://content-assets-as-bundle.decentraland.org/";
                const string HASH = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";
                const string EXPECTED = "QmfNvE3nKmahA5emnBnXN2LzydpYncHVz4xy4piw84Er1D";

                var manifest = new SceneAssetBundleManifest(URLDomain.FromString(CONTENT_URL));

                fixed (char* p = EXPECTED) { Assert.AreEqual(Hash128.Compute(p, (ulong)(EXPECTED.Length * sizeof(char))), manifest.ComputeHash(HASH)); }
            }
        }
    }
}
