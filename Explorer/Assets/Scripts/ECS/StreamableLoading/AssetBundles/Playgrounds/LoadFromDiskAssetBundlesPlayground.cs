using Cysharp.Threading.Tasks;
using DCL.Optimization.Memory;
using System.IO;
using UnityEngine;

namespace DCL.AssetsProvision.Playgrounds
{
    public class FromDiskAssetBundlesPlayground : MonoBehaviour
    {
        [SerializeField] private string dirPath;

        [ContextMenu(nameof(StartAsync))]
        public async UniTaskVoid StartAsync()
        {
            foreach (string file in Directory.EnumerateFiles(dirPath))
            {
                print($"Load file successfully: {file}");
                await using var fs = new FileStream(file, FileMode.Open);

                // 5 is meta
                fs.Seek(5, SeekOrigin.Begin);

                using var c = new MemoryChain(ISlabAllocator.SHARED);
                using var memory = new MemoryStream((int)(fs.Length - 5));
                await fs.CopyToAsync(memory);

                c.AppendData(memory.ToArray());
                using var stream = c.AsStream();

                var ab = await AssetBundle.LoadFromStreamAsync(stream);
                if (ab) ab.Unload(true);
            }
        }
    }
}
