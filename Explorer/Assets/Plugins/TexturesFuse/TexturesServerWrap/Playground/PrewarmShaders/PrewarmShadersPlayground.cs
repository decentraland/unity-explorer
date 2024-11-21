using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Platforms;
using Plugins.TexturesFuse.TexturesServerWrap.CompressShaders;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System.IO;
using System.Text;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.PrewarmShaders
{
    public class PrewarmShadersPlayground : MonoBehaviour
    {
        [SerializeField]
        private IPlatform.Kind platformKind;
        [Space]
        [SerializeField] private string pathToEmptyPng;
        [SerializeField] private bool debugBytes;

        private void Start()
        {
            if (debugBytes)
            {
                byte[] bytes = File.ReadAllBytes(pathToEmptyPng);
                var sb = new StringBuilder();

                foreach (byte b in bytes)
                {
                    sb.Append("0X");
                    sb.Append(b.ToString("X2"));
                    sb.Append(", ");
                }

                ReportHub.Log(ReportData.UNSPECIFIED, $"Bytes: {sb}");
            }

            StartAsync().Forget();
        }

        private async UniTaskVoid StartAsync()
        {
            IPlatform platform = new ConstPlatform(platformKind);
            ICompressShaders compressShaders = new CompressShaders.CompressShaders(() => ITexturesFuse.NewDefault(), platform);
            ReportHub.Log(ReportData.UNSPECIFIED, $"Shaders are ready: {compressShaders.AreReady()}");

            var timer = System.Diagnostics.Stopwatch.StartNew();
            await compressShaders.WarmUpIfRequiredAsync(destroyCancellationToken);
            timer.Stop();
            ReportHub.Log(ReportData.UNSPECIFIED, $"Warm up took: {timer.ElapsedMilliseconds}ms");
        }
    }
}
