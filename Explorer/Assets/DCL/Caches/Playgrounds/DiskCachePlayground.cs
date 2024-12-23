using Cysharp.Threading.Tasks;
using DCL.Caches.Disk;
using System.IO;
using UnityEngine;

namespace DCL.Caches.Playgrounds
{
    public class DiskCachePlayground : MonoBehaviour
    {
        [SerializeField] private string cacheDirectory = string.Empty;
        [SerializeField] private string testFile = string.Empty;

        private void Start()
        {
            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            byte[] testData = await File.ReadAllBytesAsync(testFile, destroyCancellationToken)!;
            string testExtension = Path.GetExtension(testFile);

            var diskCache = new DiskCache(cacheDirectory);
            var result = await diskCache.PutAsync("test", testExtension, testData, destroyCancellationToken);
            print($"Put result: success {result.AsResult().Success} and error {result.AsResult().ErrorMessage}");
        }
    }
}
