using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public class NullYouTubeUrlResolver : IYouTubeUrlResolver
    {
        public UniTask<ResolvedYouTubeUrl?> ResolveAsync(string youtubeUrl, CancellationToken ct) =>
            UniTask.FromResult<ResolvedYouTubeUrl?>(null);
    }
}
