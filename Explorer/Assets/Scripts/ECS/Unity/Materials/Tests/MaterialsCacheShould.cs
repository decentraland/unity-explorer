using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using System.Linq;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class MaterialsCacheShould
    {
        private const int SIZE = MaterialsCappedCache.MIN_SIZE;

        private MaterialsCappedCache cache;
        private DestroyMaterial destroy;


        public void SetUp()
        {
            cache = new MaterialsCappedCache(SIZE, destroy = Substitute.For<DestroyMaterial>());
        }


        public void IncreaseRefCountOnCacheHit()
        {
            var key = MaterialData.CreateBasicMaterial(new TextureComponent("1", TextureWrapMode.Mirror, FilterMode.Point),
                0.5f, Color.black, true);

            Material material = CreateMaterial();

            cache.Add(key, material);

            Assert.That(cache.TryReferenceMaterial(in key, out Material cached), Is.True);
            Assert.That(cached, Is.EqualTo(material));
            Assert.That(cache.TryGetCacheEntry(in key, out (Material material, int refCount) entry), Is.True);
            Assert.That(entry.refCount, Is.EqualTo(2));
        }


        public void ProvideCacheMisses()
        {
            var key = MaterialData.CreateBasicMaterial(new TextureComponent("1", TextureWrapMode.Mirror, FilterMode.Point),
                0.5f, Color.black, true);

            Material material = CreateMaterial();

            cache.Add(key, material);

            var newKey = MaterialData.CreateBasicMaterial(new TextureComponent("2", TextureWrapMode.Repeat, FilterMode.Bilinear),
                0.2f, Color.black, true);

            Assert.That(cache.TryReferenceMaterial(in newKey, out Material cached), Is.False);
            Assert.That(cached, Is.Null);
            Assert.That(cache.TryGetCacheEntry(in newKey, out (Material material, int refCount) entry), Is.False);
        }


        public void Dereference()
        {
            var key = MaterialData.CreateBasicMaterial(new TextureComponent("1", TextureWrapMode.Mirror, FilterMode.Point),
                0.5f, Color.black, true);

            Material material = CreateMaterial();

            cache.Add(key, material);

            cache.Dereference(in key);

            Assert.That(cache.TryGetCacheEntry(in key, out (Material material, int refCount) entry), Is.True);
            Assert.That(entry.refCount, Is.EqualTo(0));
        }


        [Sequential]
        public void ReleaseMaterialIfSizeIsExceeded([Values(17, 31, 40, 80, 120)] int testCount, [Values(13, 15, 16, 16, 16)] int expected)
        {
            const int RELEASE_COUNT = SIZE / 4;

            var keys = Enumerable.Range(1, testCount)
                                 .Select(i => MaterialData.CreateBasicMaterial(
                                      new TextureComponent(i.ToString(), TextureWrapMode.Mirror, FilterMode.Point),
                                      i, Color.cyan, false))
                                 .ToList();

            foreach (MaterialData data in keys)
                cache.Add(data, CreateMaterial());

            foreach (MaterialData data in keys)
                cache.Dereference(in data);

            destroy.Received(testCount - SIZE - RELEASE_COUNT);

            Assert.That(cache.Count, Is.EqualTo(expected));
        }


        public void KeepAllMaterialsWhileTheyAreReferenced([Values(4, 17, 45, 90, 125)] int count)
        {
            var keys = Enumerable.Range(1, count)
                                 .Select(i => MaterialData.CreateBasicMaterial(
                                      new TextureComponent(i.ToString(), TextureWrapMode.Mirror, FilterMode.Point),
                                      i, Color.cyan, false))
                                 .ToList();

            foreach (MaterialData data in keys)
                cache.Add(data, CreateMaterial());

            Assert.AreEqual(count, cache.Count);

            foreach (MaterialData data in keys)
                Assert.IsTrue(cache.TryGetCacheEntry(in data, out (Material material, int refCount) entry));
        }

        private static Material CreateMaterial() =>
            DefaultMaterial.New();
    }
}
