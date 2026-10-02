using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace DCL.MapRenderer.Tests.PlayMode
{
    public class BundledSatelliteChunksShould
    {
        private const int GRID_SIZE = 8;
        private const int CHUNK_PIXELS = 512;

        [UnityTest]
        public IEnumerator LoadEveryChunkFromAddressables()
        {
            for (var i = 0; i < GRID_SIZE; i++)
            for (var j = 0; j < GRID_SIZE; j++)
            {
                // Act
                AsyncOperationHandle<Texture2D> handle = Addressables.LoadAssetAsync<Texture2D>($"{i},{j}");
                yield return handle;

                // Assert
                Assert.AreEqual(AsyncOperationStatus.Succeeded, handle.Status, $"chunk {i},{j}");
                Assert.AreEqual(CHUNK_PIXELS, handle.Result.width, $"chunk {i},{j}");
                Assert.AreEqual(CHUNK_PIXELS, handle.Result.height, $"chunk {i},{j}");
                Addressables.Release(handle);
            }
        }
    }
}
