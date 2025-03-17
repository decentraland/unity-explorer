using System;
using CommunicationData.URLHelpers;
using NUnit.Framework;
using UnityEngine;

namespace SceneRunner.Scene.Tests
{
    public class SceneAssetBundleManifestShould
    {
        private const string CONTENT_URL = "https://content-assets-as-bundle.decentraland.org/";
        private const string VERSION = "v15";
        private const string SCENE_ID = "bafkreiaquafn3vdokqaf4ite7szje6255nyrudg7a4ucpdwgu543gup4fu";
        private const string BUILD_DATE = "2024-03-15T23:44:16.092Z";
        private readonly string[] originalHashes = {
            "bafkreiaryit63vshyvyddoo3dfjdapvlfcyf2jfbd6enktal3kbv2pcdru_windows",
            "bafkreicjhpml7xdib2knhbl2qn7sgqq7cudwys75xwgejdd47cxp3dkbc4_windows",
            "bafkreicljlsrh7upl5guvinmtjjqn7eagyu7e6wsef4a5nyerjuyw7t5fu_windows",
            "bafkreidgli7y7lyskioyjcgkub3ja2af7b2cj7hsjjjqvgifjk7eusixoe_windows",
            "bafkreigohcob7ium7ynqeya6ceavkkuvdndx6kjprgqah4lgpvmze6jzji_windows",
            "bafkreihm3s5xcauc6i256xnywwssnodcvtrs6z3454itsf2ph63e3tx7iq_windows"
        };

        private readonly string[] randomCaseHashes;

        private readonly SceneAssetBundleManifest sharedManifest;
        public SceneAssetBundleManifestShould()
        {
            randomCaseHashes = new string[originalHashes.Length];
            for (var i = 0; i < originalHashes.Length; i++)
            {
                randomCaseHashes[i] = originalHashes[i].ToUpper();
            }

            sharedManifest = new SceneAssetBundleManifest(URLDomain.FromString(CONTENT_URL), VERSION, originalHashes, SCENE_ID, BUILD_DATE);
        }

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

        [Test]
        public void HandlesIncorrectCasing_TryGet()
        {
            foreach (string hash in randomCaseHashes)
            {
                Assert.IsTrue(sharedManifest.TryGet(hash, out string _));
            }
        }

        [Test]
        public void HandlesIncorrectCasing_TryGet_OutString()
        {
            for(int i = 0; i < originalHashes.Length; i++)
            {
                Assert.IsTrue(sharedManifest.TryGet(randomCaseHashes[i], out string originalHash));
                Assert.AreEqual(originalHashes[i], originalHash);
            }
        }

        [Test]
        public void HandlesCorrectCasing_Contains()
        {

            foreach (string hash in originalHashes)
            {
                Assert.IsTrue(sharedManifest.Contains(hash));
            }
        }

        [Test]
        public void HandlesIncorrectCasing_Contains()
        {
            foreach (string hash in randomCaseHashes)
            {
                Assert.IsTrue(sharedManifest.Contains(hash));
            }
        }
    }
}
