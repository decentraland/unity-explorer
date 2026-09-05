using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    public interface IYouTubeUrlResolver
    {
        UniTask<ResolvedYouTubeUrl?> ResolveAsync(string youtubeUrl, CancellationToken ct);
    }
}
